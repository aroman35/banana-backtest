using System.Diagnostics;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Serilog;

namespace Banana.Backtest.Emulator.ExchangeEmulator;

public class StrategyWrapper : IStrategy
{
    private readonly ILogger _logger;
    private readonly TimeOnly _openTime = new(10, 0, 0);
    private readonly TimeOnly _closeTime = new(18, 50, 0);
    private readonly List<UserOrder> _userOrders = new();
    private readonly List<UserExecution> _userExecutions = new();
    private readonly Emulator _emulator;
    private readonly long _tradeDateOpenTimestamp;
    private readonly long _tradeDateCloseTimestamp;
    private readonly long _simulationStartedTimestamp;
    private OrderBookSnapshot _orderBook;

    private int _ordersCount;
    private double _volumeExecuted = 0;
    private double _volumeStd = 0;
    private double _volumeEwma = 0;
    private const int DataDepth = 128;

    private TradeUpdate[] _trades = new TradeUpdate[DataDepth];
    private double _lastPx;
    private int _tradesIdx;
    
    public StrategyWrapper(Emulator emulator, ILogger logger)
    {
        _emulator = emulator;
        _logger = logger.ForContext<StrategyWrapper>();
        var tradeDateOpenDateTime = new DateTime(_emulator.Hash.Date, _openTime, DateTimeKind.Local);
        var tradeDateCloseDateTime = new DateTime(_emulator.Hash.Date, _closeTime, DateTimeKind.Local);
        _tradeDateOpenTimestamp = tradeDateOpenDateTime.ToUnixTimeMilliseconds();
        _tradeDateCloseTimestamp = tradeDateCloseDateTime.ToUnixTimeMilliseconds();
        _simulationStartedTimestamp = Stopwatch.GetTimestamp();
    }

    public void OrderBookUpdated(MarketDataItem<OrderBookSnapshot> orderBookSnapshot)
    {
        if (orderBookSnapshot.Timestamp < _tradeDateOpenTimestamp || orderBookSnapshot.Timestamp > _tradeDateCloseTimestamp)
            return;
        _orderBook = orderBookSnapshot.Item;
    }

    public unsafe void AnonymousTradeReceived(MarketDataItem<TradeUpdate> trade)
    {
        _lastPx = trade.Item.Price;
        if (trade.Timestamp < _tradeDateOpenTimestamp || trade.Timestamp > _tradeDateCloseTimestamp)
            return;
        _volumeExecuted += trade.Item.Volume;
        _trades[_tradesIdx++] = trade.Item;
        if (_tradesIdx == DataDepth)
        {
            var volumesSpan = stackalloc double[DataDepth];
            
            fixed (TradeUpdate* tradesPtr = _trades)
            {
                for (var i = 0; i < DataDepth; i++)
                {
                    volumesSpan[i] = tradesPtr[i].Volume;
                }
            }

            _volumeStd = StandardDeviation(volumesSpan, DataDepth);
            _volumeEwma = Ewma(volumesSpan, DataDepth, 0.27);
            _tradesIdx = 0;
            var isLong = Math.Log10(_volumeStd).IsGreater(Math.Log10(_volumeEwma));
            var order = new UserOrder
            {
                Price = isLong ? _orderBook.Asks[0].Price : _orderBook.Bids[0].Price,
                Quantity = 2,
                Timestamp = Helpers.Timestamp,
                Side = isLong ? Side.Long : Side.Short,
                ClientOrderId = Guid.NewGuid(),
                Id = Helpers.NextId
            };
            PlaceOrder(order);
        }
    }

    public void UserExecutionReceived(UserExecution userExecution)
    {
        _userExecutions.Add(userExecution);
    }

    public void PlaceOrder(UserOrder order)
    {
        _userOrders.Add(order);
        _emulator.ProcessUserOrder(order);
    }

    public void SimulationFinished()
    {
        var remainedLimit = _userExecutions
            .Sum(execution => execution.ExecutedQuantity * (int)execution.Side);
        
        var value = remainedLimit * _lastPx;

        var total = _userExecutions
            .Sum(execution => execution.ExecutionPrice * execution.ExecutedQuantity * (int)execution.Side);

        var executedVolume = _userExecutions.Sum(execution => execution.ExecutionPrice * execution.ExecutedQuantity);
        var dirtyPnl = value - total;
        var brokerFee = executedVolume * 0.00025;
        var cleanPnl = dirtyPnl - brokerFee;
        var elapsedTime = Stopwatch.GetElapsedTime(_simulationStartedTimestamp);
        
        _logger.Information(
            "Trade date: {TradeDate} Strategy finished. Total trades: {TradesCount}, PnL: {Pnl} (Broker Fee: {Fee} | Volume: {Volume}) Duration: {Duration}",
            _emulator.Hash.Date,
            _userExecutions.Count,
            cleanPnl,
            brokerFee,
            executedVolume,
            elapsedTime);
    }

    private static unsafe double StandardDeviation(double* dataPtr, int size)
    {
        if (size == 0)
            throw new ArgumentException("Data cannot be empty.");

        double sum = 0;
        double sumSquared = 0;

        // Calculate the sum of the values
        for (var i = 0; i < size; i++)
        {
            sum += dataPtr[i];
            sumSquared += dataPtr[i] * dataPtr[i];
        }

        var mean = sum / size;
        var variance = sumSquared / size - mean * mean;
        return Math.Sqrt(variance);
    }
    
    private static unsafe double Ewma(double* dataPtr, int size, double alpha)
    {
        if (size == 0)
            throw new ArgumentException("Data cannot be empty.");

        if (alpha is <= 0 or >= 1)
            throw new ArgumentException("Alpha must be between 0 and 1 exclusive.");

        var ewma = dataPtr[0]; // Initial value
        for (var i = 1; i < size; i++)
        {
            ewma = alpha * dataPtr[i] + (1 - alpha) * ewma;
        }

        return ewma;
    }
}