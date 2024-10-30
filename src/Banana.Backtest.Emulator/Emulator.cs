using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Services;
using Banana.Backtest.Emulator.ExchangeEmulator;
using Serilog;

namespace Banana.Backtest.Emulator;

public unsafe class Emulator : IDisposable
{
    private readonly MarketDataHash _hash;
    private readonly IMarketDataCacheReader<LevelUpdate> _levelUpdatesCache;
    private readonly IMarketDataCacheReader<TradeUpdate> _tradesCache;
    private readonly ILogger _logger;
    private readonly OrderBook _orderBook;
    private readonly StrategyWrapper _emulatorGateway;
    private readonly SortedDictionary<double, UserOrder> _userBids = new();
    private readonly SortedDictionary<double, UserOrder> _userAsks = new();
    private long _currentTimestamp;

    public Emulator(
        MarketDataHash hash,
        string marketDataDirectory,
        ILogger logger)
    {
        _logger = logger
            .ForContext<Emulator>()
            .ForContext("hash", hash.ToString());
        _hash = hash;
        _emulatorGateway = new StrategyWrapper(this, logger);
        _levelUpdatesCache =
            MarketDataCacheAccessor.CreateReader<LevelUpdate>(marketDataDirectory, _hash.For(FeedType.LevelUpdates));
        _tradesCache = MarketDataCacheAccessor.CreateReader<TradeUpdate>(marketDataDirectory, _hash.For(FeedType.Trades));
        _orderBook = new OrderBook();
    }
    
    public IStrategy GetStrategy => _emulatorGateway;
    public MarketDataHash Hash => _hash;

    public void Process()
    {
        foreach (var levelUpdate in _levelUpdatesCache.ContinueReadUntil())
        {
            Interlocked.CompareExchange(ref _currentTimestamp, levelUpdate.Timestamp, 0L);
            if (levelUpdate.Timestamp > _currentTimestamp)
            {
                OrderBookUpdated();
                _currentTimestamp = levelUpdate.Timestamp;
            }
            
            _orderBook.UpdateOrder(levelUpdate);
        }
        
        _emulatorGateway.SimulationFinished();
    }

    public void ProcessUserOrder(UserOrder userOrder)
    {
        if (userOrder.OrderType == OrderType.Market)
        {
            // Немедленное исполнение рыночной заявки
            ExecuteMarketOrder(userOrder);
        }
        else if (userOrder.OrderType == OrderType.Limit)
        {
            // Добавление лимитной заявки в стакан или её немедленное исполнение
            ExecuteLimitOrder(userOrder);
        }
    }

    private void ExecuteMarketOrder(UserOrder userOrder)
    {
        if (userOrder.Side == Side.Long)
        {
            // Исполнение по текущей лучшей цене предложения (ask)
            var bestAsk = _orderBook.BestAsk;
            if (bestAsk.Quantity > 0)
            {
                var executedQuantity = Math.Min(userOrder.Quantity, bestAsk.Quantity);
                var execution = UserExecution.OrderPartiallyFill(&userOrder, executedQuantity);

                // Отправка отчёта об исполнении и обновление стакана
                _emulatorGateway.UserExecutionReceived(execution);
                var levelUpdate = new MarketDataItem<LevelUpdate>
                {
                    Timestamp = Helpers.Timestamp,
                    Item = new LevelUpdate
                    {
                        Price = bestAsk.Price,
                        Quantity = bestAsk.Quantity - executedQuantity,
                        IsBid = false,

                    }
                };
                _orderBook.UpdateOrder(levelUpdate);
            }
            else
            {
                // Если нет ликвидности для исполнения
                Console.WriteLine("No available ask orders for market buy order.");
            }
        }
        else if (userOrder.Side == Side.Short)
        {
            // Исполнение по текущей лучшей цене спроса (bid)
            var bestBid = _orderBook.BestBid;
            if (bestBid.Quantity > 0)
            {
                var executedQuantity = Math.Min(userOrder.Quantity, bestBid.Quantity);
                var execution = UserExecution.OrderPartiallyFill(&userOrder, executedQuantity);

                // Отправка отчёта об исполнении и обновление стакана
                _emulatorGateway.UserExecutionReceived(execution);
                var levelUpdate = new MarketDataItem<LevelUpdate>
                {
                    Timestamp = _currentTimestamp,
                    Item = new LevelUpdate
                    {
                        Price = bestBid.Price,
                        Quantity = bestBid.Quantity - executedQuantity,
                        IsBid = true,
                    }
                };
                _orderBook.UpdateOrder(levelUpdate);
            }
            else
            {
                // Если нет ликвидности для исполнения
                Console.WriteLine("No available bid orders for market sell order.");
            }
        }
    }

    private void ExecuteLimitOrder(UserOrder userOrder)
    {
        if (userOrder.Side == Side.Long)
        {
            // Лимитная покупка: проверить наличие предложения (ask) по желаемой цене
            if (_orderBook.BestAsk.Price <= userOrder.Price)
            {
                var bestAsk = _orderBook.BestAsk;
                var executedQuantity = Math.Min(userOrder.Quantity, bestAsk.Quantity);
                var execution = UserExecution.OrderPartiallyFill(&userOrder, executedQuantity);

                // Обновить состояние стакана
                _orderBook.UpdateOrder(new MarketDataItem<LevelUpdate>
                {
                    Timestamp = _currentTimestamp,
                    Item = new LevelUpdate
                    {
                        Price = bestAsk.Price,
                        Quantity = bestAsk.Quantity - executedQuantity,
                        IsBid = false
                    }
                });

                // Отправить отчёт о частичном или полном исполнении
                _emulatorGateway.UserExecutionReceived(execution);
            }
            else
            {
                // Если цена не соответствует, заявка добавляется в очередь как лимитная заявка
                _userBids.Add(userOrder.Price, userOrder);
            }
        }
        else if (userOrder.Side == Side.Short)
        {
            // Лимитная продажа: проверить наличие спроса (bid) по желаемой цене
            if (_orderBook.BestBid.Price >= userOrder.Price)
            {
                var bestBid = _orderBook.BestBid;
                var executedQuantity = Math.Min(userOrder.Quantity, bestBid.Quantity);
                var execution = UserExecution.OrderPartiallyFill(&userOrder, executedQuantity);

                // Обновить состояние стакана
                _orderBook.UpdateOrder(new MarketDataItem<LevelUpdate>(new LevelUpdate
                {
                    Price = bestBid.Price,
                    Quantity = bestBid.Quantity - executedQuantity,
                    IsBid = true,
                }, _currentTimestamp));

                // Отправить отчёт о частичном или полном исполнении
                _emulatorGateway.UserExecutionReceived(execution);
            }
            else
            {
                // Если цена не соответствует, заявка добавляется в очередь как лимитная заявка
                _userAsks.Add(userOrder.Price, userOrder);
            }
        }
    }

    private void OrderBookUpdated()
    {
        foreach (var trade in _tradesCache.ContinueReadUntil(_currentTimestamp))
        {
            _emulatorGateway.AnonymousTradeReceived(trade);
        }

        _emulatorGateway.OrderBookUpdated(new MarketDataItem<OrderBookSnapshot>(_orderBook.TakeSnapshot(), _currentTimestamp));
    }

    private void MatchTaker()
    {
    }

    private void RecalculateExecutionLevels()
    {
    }

    public void Dispose()
    {
        _levelUpdatesCache.Dispose();
        _tradesCache.Dispose();
    }
}