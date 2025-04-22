using System.Collections.Concurrent;
using System.Diagnostics;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;
using Banana.Backtest.Emulator.ExchangeEmulator;
using Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy.Runtime;
using Banana.Backtest.Emulator.Services;
using Banana.Backtest.Launcher.Launchers;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Xunit.Abstractions;

namespace Banana.Strategies.Predictive.UnitTests;

public class BacktestMatcherEndToEndTest(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task Test()
    {
        var matcherSettings = new MatcherSettings
        {
            PriceStep = 0.001D,
            PointPrice = 9000.0D,
            WarrantyCoverageLong = 8600.0D,
            WarrantyCoverageShort = 8500.0D,
            MakerFee = 0.00025D,
            TakerFee = 0.00025D
        };
        var marketDataSettings = new MarketDataSettings
        {
            Ticker = "NGU4",
            ClassCode = "SPBFUT",
            Exchange = Exchange.MoexFutures,
            MarketDataDirectory = "D:/market-data-storage",
            TradeDate = new DateOnly(2024, 09, 05)
        };
        await BacktestHost.CreateHost<TestingStrategy>(
            matcherSettings,
            marketDataSettings,
            CancellationToken.None,
            logger => logger.TestOutput(
                testOutputHelper,
                LogEventLevel.Information,
                "{Timestamp:yyyy-MM-ddTHH:mm:ss.ffffffZ}|{Level:u3}|{Message:lj} [{SourceContext}]{NewLine}{Exception}"));
    }

    private class TestingStrategy(
        IChannelsProvider channelsProvider,
        TimeProvider timeProvider,
        ILogger logger)
        : StrategyBase(channelsProvider, timeProvider, logger), IHostedService
    {
        private readonly ILogger _logger = logger.ForContext<TestingStrategy>();
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly RuntimeReversal _runtime = new(0.005);
        private readonly TimeOnly _startTradingDay = new(07, 00);
        private readonly TimeOnly _endTradingDay = new(16, 45);
        private readonly bool _trendReverse = true;
        private readonly double _warrantyCoverage = 8408;
        private readonly double _pointPrice = 8592;
        private readonly double _maxFundsAmount = 1_000_000.0;
        private readonly double _feeRate = 0.00025;
        private readonly ConcurrentDictionary<long, UserExecution> _executions = new();

        private AdvancedRiskManagementModule? _riskManagementModule;

        private long _mdTimestamp;
        private int _totalTradeCount;
        private int _totalOrderBooksCount;

        private long _orderId = Stopwatch.GetTimestamp();
        private double _midPrice = double.NaN;
        private Side _currentTrend = Side.Long;
        private long _timestamp;
        private DateTime CurrentTime => _timestamp.AsDateTime();
        private double _currentPositionLimit;
        private Side PositionSide => (Side)Math.Sign(_currentPositionLimit);
        private double _lastPx;
        private FastExecution? _closingExecution;

        private LinkedList<TrendReversal> _trendReversals = new();

        public override ValueTask HandleChannelDataAsync(UserExecution channelData, CancellationToken cancellationToken)
        {
            _logger.Information(
                "Execution received: [{Side}] {Price}x{Quantity} [{ClientOrderId}]",
                channelData.Side,
                channelData.ExecutionPrice,
                channelData.ExecutedQuantity,
                channelData.ClientOrderId);

            _currentPositionLimit += channelData.ExecutedQuantity * (int)channelData.Side;
            _executions.TryAdd(channelData.OrderId, channelData);

            return ValueTask.CompletedTask;
        }

        public override ValueTask HandleChannelDataAsync(OrderInfo channelData, CancellationToken cancellationToken)
        {
            _logger.Information(
                "Order update received: {Status} ts: {ExchangeTimestamp}",
                channelData.Status,
                channelData.StatusUpdateTimestamp);
            return ValueTask.CompletedTask;
        }

        private readonly SemaphoreSlim _semaphore = new(1, 1);
        public override async ValueTask HandleChannelDataAsync(MarketDataItem channelData, CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            if (channelData.IsTrade)
            {
                _totalTradeCount++;
                await AnonymousTradeReceivedWithRiskManager(channelData.Trade);
            }
            else
            {
                _totalOrderBooksCount++;
                _midPrice = channelData.OrderBook.Item.MidPrice;
            }

            if (channelData.Timestamp < _mdTimestamp)
            {
                Logger.Warning(
                    "Timestamps unmatch for {MdType}, SystemTime: {SystemTime:O} | md Time: {TradeTime:O} | Last Committed Time: {LastCommittedTime:O}",
                    channelData.IsTrade ? "TRADE" : "ORDER-BOOK",
                    _timeProvider.GetUtcNow(),
                    channelData.Timestamp.AsDateTime(),
                    _mdTimestamp.AsDateTime());
            }
            _mdTimestamp = channelData.Timestamp;
            _semaphore.Release();
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await ClosePosition();
            Logger.Information(
                "Total trades count: {TradesCount}, order-books count: {OrderBooksCount}",
                _totalTradeCount,
                _totalOrderBooksCount);
            var entry = _trendReversals.First;
            var matchedTrends = 0;
            var handledTrends = 0;
            var totalDelta = 0.0D;
            var approximateFee = 0.0D;
            while (entry is not null)
            {
                var nextEntry = entry.Next;
                if (nextEntry is null)
                    break;
                var delta = (nextEntry.Value.Price - entry.Value.Price) * (int)entry.Value.Side;
                totalDelta += delta;
                ++handledTrends;
                approximateFee += _feeRate * 2 * entry.Value.Price;

                if (delta.IsGreaterOrEquals(0.0D))
                    ++matchedTrends;

                entry = nextEntry;
            }

            Logger.Information(
                "Pure quality: {Quality}% [{Success}/{Total}]. Delta sum: {TotalDelta} Fee: {Fee} Clean: {Clean}",
                handledTrends > 0 ? double.Round(1.0D * matchedTrends / handledTrends * 100) : 0,
                matchedTrends,
                handledTrends,
                totalDelta,
                approximateFee,
                (totalDelta - approximateFee) * _pointPrice
                );
            // await base.StopAsync(cancellationToken);
            var pnl = _executions.Values.Sum(execution => execution.ExecutionPrice * execution.ExecutedQuantity * -(int)execution.Side) * _pointPrice;
            var fee = _executions.Values.Sum(execution => execution.ExecutionPrice * execution.ExecutedQuantity) * _pointPrice * _feeRate;
            // var matches = _trendReversals
            //     .Select(x =>
            //     {
            //         var signalPrice = x.Price;
            //         var executions = _executions.Values.Where(t => t.ClientOrderId == x.ClientOrderId).ToList();
            //         var executionPrice = executions.Average(t => t.ExecutionPrice);
            //         var side = x.Side;
            //         return new
            //         {
            //             SignalTime = x.Timestamp,
            //             SignalPrice = signalPrice,
            //             ExecutionPrice = executionPrice,
            //             ExecutedQuantity = executions.Sum(t => t.ExecutedQuantity),
            //             Side = side,
            //             ExecutionDelta = double.Abs(signalPrice - executionPrice),
            //             IsExecuted = executions.Count() != 0
            //         };
            //     })
            //     .ToArray();
            // var orderDeltas = _trendReversals
            //     .ToDictionary(
            //         x => x.ClientOrderId,
            //         x =>
            //         {
            //             if (_executions.Values.Count(t => t.ClientOrderId == x.ClientOrderId) == 0)
            //                 return 0;
            //             var delta = _executions.Values.FirstOrDefault(t => t.ClientOrderId == x.ClientOrderId).ExecutionPrice - x.Price;
            //             return double.Round(delta, 3);
            //         });
            Logger.Information("Total PnL: {PnL} ({Clean}) in {TradesCount} trades, opened qty: {Opened}", pnl, pnl - fee, _executions.Values.Count, _currentPositionLimit);
        }

        private async ValueTask AnonymousTradeReceivedWithRiskManager(MarketDataItem<TradeUpdate> trade)
        {
            var currentTrendUpdate = (Side)(_runtime.FindTrendReversal(trade.Item.Price) * (_trendReverse ? -1 : 1));
            var trendChanged = _currentTrend != currentTrendUpdate;

            _timestamp = trade.Timestamp;
            _lastPx = trade.Item.Price;

            if (currentTrendUpdate is Side.Undefined)
                return;

            if (trendChanged)
            {
                _logger.Information("[{Time}] Trend changed {Prev} -> {Trend} at price {Price}", CurrentTime, _currentTrend, currentTrendUpdate, _lastPx);
                var clientOrderId = Guid.NewGuid();
                _trendReversals.AddLast(new TrendReversal(_timeProvider.GetUtcNow().DateTime, _lastPx, currentTrendUpdate, clientOrderId));
                var quantity = _currentPositionLimit.IsEquals(0.0D) ? 1.0D : 2.0D;
                var execution = new FastExecution(channelsProvider, _timeProvider, new FastExecutionSettings
                {
                    PriceLimit = _lastPx,
                    PriceSpread = 1.0D,
                    RequestedQuantity = quantity,
                    Side = currentTrendUpdate
                }, Logger);
                _ = Task.Run(() => execution.ExecutionCompletion);
                // await PlaceOrder(new PlaceOrderRequest
                // {
                //     Side = currentTrendUpdate,
                //     ClientOrderId = clientOrderId,
                //     OrderType = OrderType.Market,
                //     Price = _lastPx,
                //     Quantity = quantity
                // });
            }
            _currentTrend = currentTrendUpdate;
            if (CurrentTime.Time() > _endTradingDay)
            {
                await ClosePosition();
            }

            // if (CurrentTime.Time() < _startTradingDay)
            //     return;
            //
            // if (CurrentTime.Time() > _endTradingDay)
            // {
            //     _riskManagementModule = null;
            //     if (!_currentPositionLimit.IsEquals(0.0) && _closingExecution is null)
            //         await ClosePosition();
            //     return;
            // }
            //
            // if (trendChanged || _riskManagementModule is not null)
            // {
            //     await ProcessOrderForRiskManager(trade.Item.Price);
            // }
        }

        private async ValueTask ProcessOrderForRiskManager(double price)
        {
            _riskManagementModule ??= new AdvancedRiskManagementModule(new RiskManagementSettings(
                _currentTrend,
                price,
                2,
                0.001,
                _feeRate,
                0.3,
                _warrantyCoverage,
                _maxFundsAmount,
                _pointPrice,
                0.001,
                3));

            if (_riskManagementModule.CreateOrderRequest(price))
            {
                switch (_riskManagementModule.Status)
                {
                    case RiskStatus.OrderRequest:
                        await PlaceOrder(new PlaceOrderRequest
                        {
                            ClientOrderId = Guid.NewGuid(),
                            OrderType = OrderType.Limit,
                            Price = _riskManagementModule.PlannedOrderPrice,
                            Quantity = _riskManagementModule.PlannedOrderQuantity,
                            Side = _riskManagementModule.Side,
                        });
                        break;
                    case RiskStatus.CloseRequest:
                        _riskManagementModule = null;
                        await ClosePosition();
                        break;
                    case RiskStatus.Finished:
                        _riskManagementModule = null;
                        break;
                }
            }
        }

        private async ValueTask ClosePosition()
        {
            if (_currentPositionLimit.IsEquals(0.0))
                return;

            _logger.Debug(
                "[{Time}] Closing position: {Side} {Qty}",
                CurrentTime,
                PositionSide,
                Math.Abs(_currentPositionLimit));

            // Task.Run(async () =>
            // {
            //     _closingExecution = new FastExecution(channelsProvider, timeProvider, new FastExecutionSettings
            //     {
            //         PriceLimit = _midPrice,
            //         PriceSpread = 1.0,
            //         Side = (Side)(-Math.Sign(_currentPositionLimit)),
            //         RequestedQuantity = Math.Abs(_currentPositionLimit)
            //     }, Logger);
            //     await _closingExecution.ExecutionCompletion.ContinueWith((_, _) =>
            //     {
            //         Logger.Information("Closing execution finished");
            //         _closingExecution = null;
            //     }, null);
            // });

            var orderRequest = new PlaceOrderRequest
            {
                ClientOrderId = Guid.NewGuid(),
                OrderType = OrderType.Market,
                Price = _midPrice,
                Quantity = Math.Abs(_currentPositionLimit),
                Side = (Side)(-Math.Sign(_currentPositionLimit))
            };
            await PlaceOrder(orderRequest);
        }

        private record struct TrendReversal(DateTime Timestamp, double Price, Side Side, Guid ClientOrderId);
    }
}
