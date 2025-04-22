using System.Diagnostics;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.Contracts;
using Microsoft.ML;
using Serilog;

namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy.Runtime;

public class LazyStrategyWrapper : IStrategy
{
    private readonly ILogger _logger;
    private readonly RuntimeReversal _runtime = new(0.005);
    private readonly LazyStrategyFeaturesBuilder _featuresBuilder;
    private readonly ModelHandler _modelHandler = new(new MLContext(seed: 0));
    private readonly Emulator _emulator;
    private readonly TimeOnly _startTradingDay = new(07, 00);
    private readonly TimeOnly _endTradingDay = new(16, 45);
    private readonly float _tradeThreshold = 0.01f;
    private readonly double _positionIncreaseThreshold = 0.002;
    private readonly double _warrantyCoverage = 8408;
    private readonly double _pointPrice = 8592;
    private readonly double _maxFundsAmount = 1_000_000.0;
    private readonly double _feeRate = 0.00025;
    private readonly bool _mlEnabled = false;
    private readonly bool _trendReverse = false;
    private readonly List<UserExecution> _executions = new();

    private long _orderId = Stopwatch.GetTimestamp();
    private double _midPrice = double.NaN;
    private Side _currentTrend = Side.Long;
    private long _timestamp;
    private DateTime CurrentTime => _timestamp.AsDateTime();
    private double _currentPositionLimit;
    private Side PositionSide => (Side)Math.Sign(_currentPositionLimit);
    private double _floatingVolume;
    private double _lastPx;
    private double _lastOrderPrice;
    private int _tradesCount;
    private long _lastReportedTimestamp;
    private double _executedVolume;
    private Guid? _closingOrderClId;

    private AdvancedRiskManagementModule? _riskManagementModule;

    public LazyStrategyWrapper(Emulator emulator, ILogger logger)
    {
        _emulator = emulator;
        _logger = logger.ForContext<LazyStrategyWrapper>();
        _featuresBuilder = new LazyStrategyFeaturesBuilder(logger);
        _modelHandler.LoadModel();
    }

    public void OrderBookUpdated(MarketDataItem<OrderBookSnapshot> orderBookSnapshot)
    {
        _midPrice = orderBookSnapshot.Item.MidPrice;
        _timestamp = orderBookSnapshot.Timestamp;

        if (_timestamp - _lastReportedTimestamp  > 3600_000)
        {
            _lastReportedTimestamp = _timestamp;
            _logger.Debug("Time: {Time}", CurrentTime);
        }

        if (CurrentTime.Time() < _startTradingDay)
            return;

        _featuresBuilder.OrderBookUpdated(orderBookSnapshot, out _);
    }

    public void AnonymousTradeReceived(MarketDataItem<TradeUpdate> trade)
    {
        _lastPx = trade.Item.Price;
        AnonymousTradeReceivedWithRiskManager(trade);
        return;
        var currentTrendUpdate = (Side)(_runtime.FindTrendReversal(trade.Item.Price) * (_trendReverse ? -1 : 1));
        var trendChangedForPosition = PositionSide != currentTrendUpdate;
        var trendChanged = _currentTrend != currentTrendUpdate;
        if (trendChanged)
            _logger.Debug("[{Time}] Trend changed {Prev} -> {Trend} at price {Price}", CurrentTime, _currentTrend, currentTrendUpdate, _lastPx);

        _currentTrend = currentTrendUpdate;
        _timestamp = trade.Timestamp;

        if (trendChangedForPosition && !_currentPositionLimit.IsEquals(0) && _closingOrderClId is null)
        {
            ClosePosition();
        }

        var priceChange = (trade.Item.Price - _lastOrderPrice) * (int)PositionSide / _lastOrderPrice;

        if (!_currentPositionLimit.IsEquals(0)
            && !double.IsNaN(_lastOrderPrice)
            && !trendChangedForPosition
            && priceChange.IsGreater(_positionIncreaseThreshold))
        {
            PlaceOrder(new UserOrder
            {
                ClientOrderId = Guid.NewGuid(),
                Id = Interlocked.Increment(ref _orderId),
                Side = PositionSide,
                Price = _midPrice,
                OrderType = OrderType.Market,
                Quantity = Math.Abs(_currentPositionLimit),
                Timestamp = _timestamp
            });
        }

        if (CurrentTime.Time() < _startTradingDay)
            return;

        if (CurrentTime.Time() > _endTradingDay)
        {
            if (!_currentPositionLimit.IsEquals(0.0) && _closingOrderClId is not null)
                ClosePosition();
            return;
        }

        if (_mlEnabled && _featuresBuilder.AnonymousTradeReceived(trade, out var features) && features is not null && features.IsValid())
        {
            if (_currentTrend is Side.Undefined)
                return;
            var prediction = _modelHandler.Predict(features);
            if (prediction.Score > _tradeThreshold && !double.IsNaN(_midPrice))
            {
                PlaceOrder(new UserOrder
                {
                    ClientOrderId = Guid.NewGuid(),
                    Id = Interlocked.Increment(ref _orderId),
                    Side = _currentTrend,
                    Price = _midPrice,
                    OrderType = OrderType.Market,
                    Quantity = 1,
                    Timestamp = _timestamp
                });
            }
        }

        if (!_mlEnabled && trendChanged && _currentTrend is not Side.Undefined)
        {
            _logger.Verbose("[{Time}] Trend changed -> {Trend} at price {Price}", CurrentTime, _currentTrend, _lastPx);

            PlaceOrder(new UserOrder
            {
                ClientOrderId = Guid.NewGuid(),
                Id = Interlocked.Increment(ref _orderId),
                Side = _currentTrend,
                Price = _midPrice,
                OrderType = OrderType.Market,
                Quantity = 1,
                Timestamp = _timestamp
            });
        }
    }

    private void AnonymousTradeReceivedWithRiskManager(MarketDataItem<TradeUpdate> trade)
    {
        var currentTrendUpdate = (Side)(_runtime.FindTrendReversal(trade.Item.Price) * (_trendReverse ? -1 : 1));
        var trendChanged = _currentTrend != currentTrendUpdate;

        _timestamp = trade.Timestamp;
        _lastPx = trade.Item.Price;

        if (currentTrendUpdate is Side.Undefined)
            return;

        if (trendChanged)
            _logger.Debug("[{Time}] Trend changed {Prev} -> {Trend} at price {Price}", CurrentTime, _currentTrend, currentTrendUpdate, _lastPx);
        _currentTrend = currentTrendUpdate;

        if (CurrentTime.Time() < _startTradingDay)
            return;

        if (CurrentTime.Time() > _endTradingDay)
        {
            _riskManagementModule = null;
            if (!_currentPositionLimit.IsEquals(0.0) && _closingOrderClId is not null)
                ClosePosition();
            return;
        }

        if (!_mlEnabled && trendChanged)
        {
            ProcessOrderForRiskManager(trade.Item.Price);
        }

        if (_mlEnabled && _featuresBuilder.AnonymousTradeReceived(trade, out var features) && features is not null && features.IsValid())
        {
            if (_currentTrend is Side.Undefined)
                return;
            var prediction = _modelHandler.Predict(features);
            if (prediction.Score > _tradeThreshold && !double.IsNaN(_midPrice))
            {
                if (_riskManagementModule is not null)
                    ClosePosition();

                _riskManagementModule = new AdvancedRiskManagementModule(new RiskManagementSettings(
                    _currentTrend,
                    _midPrice,
                    2,
                    0.001,
                    _feeRate,
                    0.3,
                    _warrantyCoverage,
                    _maxFundsAmount,
                    _pointPrice,
                    0.001,
                    3));

                PlaceOrder(new UserOrder
                {
                    ClientOrderId = Guid.NewGuid(),
                    Id = Interlocked.Increment(ref _orderId),
                    Side = _currentTrend,
                    Price = _midPrice,
                    OrderType = OrderType.Market,
                    Quantity = 1,
                    Timestamp = _timestamp
                });
            }
        }
        else
        {
            ProcessOrderForRiskManager(trade.Item.Price);
        }
    }

    public void UserExecutionReceived(UserExecution userExecution)
    {
        _riskManagementModule?.ExecutionReceived(userExecution);
        _executions.Add(userExecution);

        _currentPositionLimit += userExecution.ExecutedQuantity * (int)userExecution.Side;
        var volume = userExecution.ExecutedQuantity * userExecution.ExecutionPrice;
        _floatingVolume -= (int)userExecution.Side * volume;
        _executedVolume += volume;
        _tradesCount++;
        _lastOrderPrice = userExecution.ExecutionPrice;
        if (_closingOrderClId is not null && _closingOrderClId == userExecution.ClientOrderId)
        {
            _closingOrderClId = null;
            _logger.Debug("[{Time}] Position closed", CurrentTime);
        }
        _logger.Debug(
            "[{Time}] Received execution: {Side} {Price}x{Quantity}",
            CurrentTime,
            userExecution.Side,
            userExecution.ExecutionPrice,
            userExecution.ExecutedQuantity);
    }

    public void PlaceOrder(UserOrder order)
    {
        _logger.Verbose(
            "[{Time}] Order {Side} {Price}x{Quantity} requested",
            _timestamp.AsDateTime(),
            order.Side,
            order.Price,
            order.Quantity);
        if (Math.Abs(_currentPositionLimit) + order.Quantity > _maxFundsAmount / _warrantyCoverage && PositionSide == order.Side)
        {
            _logger.Debug("[{Time}] No enough funds for order", CurrentTime);
            return;
        }
        if (_closingOrderClId is not null && _closingOrderClId != order.ClientOrderId)
        {
            _logger.Debug("[{Time}] Position is in closing state", CurrentTime);
            return;
        }
        _emulator.ProcessUserOrder(order);
        _logger.Debug(
            "[{Time}] Order {Side} {Price}x{Quantity} placed",
            CurrentTime,
            order.Side,
            order.Price,
            order.Quantity);
    }

    public void SimulationFinished()
    {
        if (_tradesCount == 0)
            return;
        _floatingVolume -= _currentPositionLimit * _lastPx;
        var (unrealizedPnl, realizedPnl) = CalculateTotalPnL(_executions, _lastOrderPrice, _pointPrice, _feeRate);
        _logger.Information(
            "[{Time}]: Total PnL: {Pnl}. Opened quantity: {OpenedQuantity}, total trades: {TotalTrades}, Vol: {Volume}, BrokerFee: {BrokerFee}",
            CurrentTime,
            double.Round(unrealizedPnl + realizedPnl, 2),
            _currentPositionLimit,
            _tradesCount,
            double.Round(_executedVolume * _pointPrice, 2),
            double.Round(_executedVolume * _feeRate * _pointPrice, 2));
    }

    private void ClosePosition()
    {
        if (_currentPositionLimit.IsEquals(0.0))
            return;
        _logger.Debug(
            "[{Time}] Closing position: {Side} {Qty}",
            CurrentTime,
            PositionSide,
            Math.Abs(_currentPositionLimit));

        var order = new UserOrder
        {
            ClientOrderId = Guid.NewGuid(),
            Id = Interlocked.Increment(ref _orderId),
            Side = (Side)(-Math.Sign(_currentPositionLimit)),
            Price = _midPrice,
            OrderType = OrderType.Market,
            Quantity = Math.Abs(_currentPositionLimit),
            Timestamp = _timestamp
        };
        _closingOrderClId = order.ClientOrderId;
        PlaceOrder(order);
    }

    private void ProcessOrderForRiskManager(double price)
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
                    PlaceOrder(new UserOrder
                    {
                        ClientOrderId = Guid.NewGuid(),
                        Id = Interlocked.Increment(ref _orderId),
                        OrderType = OrderType.Market,
                        Price = _riskManagementModule.PlannedOrderPrice,
                        Quantity = _riskManagementModule.PlannedOrderQuantity,
                        Side = _riskManagementModule.Side,
                        Timestamp = _timestamp
                    });
                    break;
                case RiskStatus.CloseRequest:
                    _riskManagementModule = null;
                    ClosePosition();
                    break;
                case RiskStatus.Finished:
                    _riskManagementModule = null;
                    break;
            }
        }
    }

    public static (double UnrealizedPnl, double RealizedPnl) CalculateTotalPnL(
    List<UserExecution> executions,
    double lastPrice,
    double pointPrice,
    double commissionRate = 0.0)
    {
        if (executions == null || executions.Count == 0)
            return (0.0, 0.0);

        // Сортируем исполнения по времени (Timestamp) для применения FIFO
        executions.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        // Список открытых лотов: каждый элемент хранит (Side, Quantity, Price)
        var openLots = new List<(Side Side, double Quantity, double Price)>();
        double realizedPnl = 0.0;

        foreach (var exec in executions)
        {
            // Для расчёта стороны: Long = +1, Short = -1
            int execFactor = (int)exec.Side;

            // Если нет открытых лотов, добавляем текущее исполнение как новый лот.
            if (openLots.Count == 0)
            {
                openLots.Add((exec.Side, exec.ExecutedQuantity, exec.ExecutionPrice));
                continue;
            }

            // Определяем текущую чистую позицию по количеству с учётом стороны
            double netQuantity = 0.0;
            foreach (var lot in openLots)
                netQuantity += (int)lot.Side * lot.Quantity;

            // Если позиция уже закрыта, начинаем новый лот.
            if (Math.Abs(netQuantity) < 1e-8)
            {
                openLots.Add((exec.Side, exec.ExecutedQuantity, exec.ExecutionPrice));
                continue;
            }

            // Определяем сторону текущей открытой (нетто) позиции: +1 если Long, -1 если Short
            int netSide = netQuantity > 0 ? 1 : -1;

            if (execFactor == netSide)
            {
                // Исполнение в ту же сторону – увеличиваем позицию.
                openLots.Add((exec.Side, exec.ExecutedQuantity, exec.ExecutionPrice));
            }
            else
            {
                // Исполнение противоположной стороны – оно закрывает часть или всю позицию.
                double remainingQty = exec.ExecutedQuantity;
                while (remainingQty > 1e-8 && openLots.Count > 0)
                {
                    var lot = openLots[0];
                    double qtyClosed = 0.0;
                    if (remainingQty >= lot.Quantity)
                    {
                        qtyClosed = lot.Quantity;
                        openLots.RemoveAt(0);
                    }
                    else
                    {
                        qtyClosed = remainingQty;
                        // Обновляем лот с уменьшенным количеством.
                        openLots[0] = (lot.Side, lot.Quantity - qtyClosed, lot.Price);
                    }

                    // Расчёт разницы цены для закрытия лота.
                    double pnlForLot = 0.0;
                    if (lot.Side == Side.Long)
                        pnlForLot = (exec.ExecutionPrice - lot.Price) * qtyClosed;
                    else // для Short
                        pnlForLot = (lot.Price - exec.ExecutionPrice) * qtyClosed;

                    // Расчёт комиссии: комиссия с входа и выхода
                    double commissionFee = (lot.Price + exec.ExecutionPrice) * qtyClosed * commissionRate;

                    // Корректировка pnl с вычетом комиссии
                    pnlForLot -= commissionFee;
                    realizedPnl += pnlForLot;

                    remainingQty -= qtyClosed;
                }
                // Если после закрытия лотов остаётся неиспользованное количество – оно открывает новый лот.
                if (remainingQty > 1e-8)
                    openLots.Add((exec.Side, remainingQty, exec.ExecutionPrice));
            }
        }

        // Расчёт нереализованного PnL по оставшимся открытым лотам
        double unrealizedPnl = 0.0;
        foreach (var lot in openLots)
        {
            if (lot.Side == Side.Long)
                unrealizedPnl += (lastPrice - lot.Price) * lot.Quantity;
            else // для Short
                unrealizedPnl += (lot.Price - lastPrice) * lot.Quantity;
        }

        // Масштабируем полученные значения с учетом PointPrice и округляем до 2 знаков
        realizedPnl = Math.Round(realizedPnl * pointPrice, 2);
        unrealizedPnl = Math.Round(unrealizedPnl * pointPrice, 2);

        return (unrealizedPnl, realizedPnl);
    }
}
