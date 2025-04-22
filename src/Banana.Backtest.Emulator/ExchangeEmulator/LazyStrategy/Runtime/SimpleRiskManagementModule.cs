using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Emulator.Contracts;

namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy.Runtime;

// Status | Step | Next trade price | Stop price
public class SimpleRiskManagementModule : IRiskManagementModule
{
    private readonly List<RiskReportItem> _results = new();
    private readonly RiskManagementSettings _settings;
    private readonly ConcurrentDictionary<int, RiskReportItem> _reportsByStep = new();
    private readonly MidpointRounding _midpointRounding;

    private double _maxDrawDown = 0.0D;
    private double _maxPnl = 0.0D;
    private int _ordersCount = 0;
    private int _currentStep = 0;
    private double _balanceQuantity = 0.0D;
    private double _balanceAmount = 0.0D;
    private double _lastPrice;
    private double _feeExecuted = 0.0D;

    public Side Side => _settings.Side;
    public RiskStatus Status { get; private set; }
    public double PlannedOrderPrice { get; private set; }
    public double PlannedOrderQuantity { get; private set; }
    public double BalancePrice { get; private set; }
    public double BalanceQuantity => _balanceQuantity;
    public double StopPrice { get; private set; }
    public double CurrentUnrealizedPnl { get; private set; }
    public double FeeExecuted => double.Round(BalancePrice * BalanceQuantity * _settings.FeeRate * _settings.PointPrice, 2, _midpointRounding);

    public SimpleRiskManagementModule(RiskManagementSettings settings)
    {
        _settings = settings;
        _midpointRounding = _settings.Side is Side.Long ? MidpointRounding.ToNegativeInfinity : MidpointRounding.ToPositiveInfinity;
        CalculateRiskSchema();
        _lastPrice = settings.StartPrice;
        PlannedOrderPrice = _settings.StartPrice;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool CreateOrderRequest(double currentPrice)
    {
        StopPrice = ReCalculateStopPrice(currentPrice);
        CurrentUnrealizedPnl = RecalculateUnrealizedPnl(currentPrice);

        if (((StopPrice - currentPrice) * (int)Side).IsGreaterOrEquals(0.0D))
        {
            Status = RiskStatus.CloseRequest;
            return true;
        }

        if (Status is RiskStatus.Finished)
            return false;

        var (price, quantity) = NextLevel(_currentStep);
        PlannedOrderPrice = price;
        PlannedOrderQuantity = quantity;


        if (((currentPrice - PlannedOrderPrice) * (int)Side).IsGreaterOrEquals(0.0D))
        {
            var planedWarrantyCoverage = (_balanceQuantity + PlannedOrderQuantity) * _settings.ContractMargin;
            if (planedWarrantyCoverage.IsGreater(_settings.Balance))
            {
                return false;
            }
            _currentStep++;
            Status = RiskStatus.OrderRequest;
            return true;
        }


        Status = RiskStatus.Waiting;
        return false;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void ExecutionReceived(UserExecution execution)
    {
        var multiplier = execution.Side == _settings.Side ? 1 : -1;
        _balanceQuantity += execution.ExecutedQuantity;
        _balanceAmount += execution.ExecutedQuantity * execution.ExecutionPrice * _settings.PointPrice ;
        BalancePrice = double.Round(_balanceAmount / _balanceQuantity / _settings.PointPrice, _settings.PriceRoundRatio, _midpointRounding);
        StopPrice = ReCalculateStopPrice(execution.ExecutionPrice);
        CurrentUnrealizedPnl = RecalculateUnrealizedPnl(execution.ExecutionPrice);
        if (Status is RiskStatus.CloseRequest)
        {
            Status = RiskStatus.Finished;
            return;
        }
        Status = RiskStatus.Waiting;
    }

    private double ReCalculateStopPrice(double currentPrice)
    {
        var deltaPrice = BalancePrice.IsEquals(currentPrice) || _balanceQuantity.IsEquals(0.0D)
            ? NextLevel(-1).Price
            : BalancePrice;
        var priceDelta = (currentPrice - deltaPrice) * _settings.LossRate * (int)_settings.Side;
        var updatedStopPrice = double.Round(
            currentPrice - priceDelta * (int)_settings.Side,
            _settings.PriceRoundRatio,
            _midpointRounding);

        return (updatedStopPrice - StopPrice).IsGreater(0.0D) ? updatedStopPrice : StopPrice;
    }

    private double RecalculateUnrealizedPnl(double currentPrice)
    {
        var dirtyPnl = (currentPrice - BalancePrice) * (int)_settings.Side * _settings.PointPrice * _balanceQuantity;
        return double.Round(dirtyPnl - FeeExecuted, 2, _midpointRounding);
    }

    private bool NextReport(int step, ref double balanceQuantity, ref double balanceAmount, ref double lastPrice, ref double feeExecuted, out RiskReportItem report)
    {
        report = default;
        var side = (int)_settings.Side;
        lastPrice += double.Round(lastPrice * _settings.MinPriceIncrement, _settings.PriceRoundRatio) * side;
        var (price, quantity) = NextLevel(step);
        balanceQuantity += quantity;
        balanceAmount += price * quantity;
        var balancePrice = double.Round(balanceAmount / balanceQuantity, _settings.PriceRoundRatio);
        feeExecuted += double.Round(price * quantity * _settings.PointPrice * _settings.FeeRate, _settings.PriceRoundRatio);
        var stopDrawDownMinPrice = double.Round((price - balancePrice) * side * _settings.LossRate / _settings.MinPriceIncrement);
        var absoluteStopDrawDown = double.Round((stopDrawDownMinPrice.IsEquals(0.0D) ? 1 : stopDrawDownMinPrice) * _settings.MinPriceIncrement, _settings.PriceRoundRatio);

        var stopPrice = double.Round(price - absoluteStopDrawDown * side, _settings.PriceRoundRatio);
        var stopDirtyPnl = double.Round((stopPrice - balancePrice) * side * balanceQuantity * _settings.PointPrice, _settings.PriceRoundRatio);
        var stopFee = double.Round(stopPrice * balanceQuantity * _settings.PointPrice * _settings.FeeRate, _settings.PriceRoundRatio);
        var stopCleanPnl = double.Round(stopDirtyPnl - stopFee - feeExecuted, _settings.PriceRoundRatio);
        var noLossPoint = double.Round(balancePrice * (1 + _settings.FeeRate * side) / (1 - _settings.FeeRate), _settings.PriceRoundRatio, _midpointRounding);
        var margin = double.Round(balanceQuantity * _settings.ContractMargin, _settings.PriceRoundRatio);
        var pnlPercent = double.Round(stopCleanPnl / margin, _settings.PriceRoundRatio);
        var priceIncreased = double.Round((price - _settings.StartPrice) / _settings.StartPrice, _settings.PriceRoundRatio);

        _maxDrawDown = double.Min(stopCleanPnl, _maxDrawDown);
        _maxPnl = double.Max(stopCleanPnl, _maxPnl);

        if (balanceQuantity * _settings.ContractMargin > _settings.Balance)
            return false;

        report = new RiskReportItem(
            step,
            quantity,
            price,
            balancePrice,
            stopPrice,
            stopDirtyPnl,
            stopCleanPnl,
            pnlPercent * 100,
            margin,
            noLossPoint,
            priceIncreased * 100,
            _settings.Side);

        return true;
    }

    private (double Price, double Quantity) NextLevel(int step)
    {
        var price = double.Round(
            _settings.StartPrice * double.Pow(1 + _settings.OrderIncreasePercentage * (int)_settings.Side, step),
            _settings.PriceRoundRatio,
            _midpointRounding);
        var quantity = NextQuantity(step);
        return (price, quantity);
    }

    private void CalculateRiskSchema()
    {
        var balanceQuantity =  0.0;
        var balanceAmount =  0.0;
        var lastPrice = _settings.StartPrice;
        var feeExecuted = 0.0;
        var step = 0;

        while (NextReport(step++, ref balanceQuantity, ref balanceAmount, ref lastPrice, ref feeExecuted, out var report))
        {
            Upsert(report);
        }
    }

    protected virtual Func<int, double> NextQuantity =>
        step => step == 0
            ? _settings.OrderMultiplier
            : double.Round(double.Exp2(step) / step * _settings.OrderMultiplier, 0);

    private void Upsert(RiskReportItem report)
    {
        _reportsByStep.Remove(report.Step, out _);
        _reportsByStep.TryAdd(report.Step, report);
    }
}

public readonly record struct OrderRequest(
    Side Side,
    double PlannedPrice,
    double PlannedQuantity)
{
    public static OrderRequest Empty = new();
}

public readonly record struct RiskManagementSettings(
    Side Side,
    double StartPrice,
    double OrderMultiplier,
    double MinPriceIncrement,
    double FeeRate,
    double LossRate,
    double ContractMargin,
    double Balance,
    double PointPrice,
    double OrderIncreasePercentage,
    int PriceRoundRatio
    );

public readonly record struct RiskReportItem(
    int Step,
    double PlannedQuantity,
    double PlannedPrice,
    double BalancePrice,
    double StopPrice,
    double StopDirtyPnl,
    double StopCleanPnl,
    double PnlPercent,
    double Margin,
    double NoLossPoint,
    double MarketIncome,
    Side OrderSide
)
{
    public OrderRequest Order() => new(OrderSide, PlannedPrice, PlannedQuantity);
}

public record struct RiskSignal(bool IsFired, bool IsPositionCloseRequested, OrderRequest Order)
{
    public static RiskSignal NoSignal = new(false, false, OrderRequest.Empty);
}

public enum RiskStatus
{
    NotStarted,
    Waiting,
    OrderRequest,
    CloseRequest,
    Finished
}
