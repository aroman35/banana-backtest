using System.Runtime.CompilerServices;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Emulator.Contracts;

namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy.Runtime;

public class AdvancedRiskManagementModule : IRiskManagementModule
{
    private readonly RiskManagementSettings _settings;
    private readonly MidpointRounding _rounding;
    private int _currentStep;
    private double _balanceQuantity;
    private double _balanceAmount;

    // Для trailing-stop: для Long – максимальная цена после входа; для Short – минимальная.
    private double _maxPrice;
    private double _minPrice;

    public RiskStatus Status { get; private set; }
    public double PlannedOrderPrice { get; private set; }
    public double PlannedOrderQuantity { get; private set; }
    public double BalancePrice => _balanceQuantity > 0
        ? Math.Round(_balanceAmount / _balanceQuantity / _settings.PointPrice, _settings.PriceRoundRatio, _rounding)
        : _settings.StartPrice;
    public double StopPrice { get; private set; }
    public double CurrentUnrealizedPnl { get; private set; }
    public double FeeExecuted => Math.Round(BalancePrice * _balanceQuantity * _settings.FeeRate * _settings.PointPrice, 2, _rounding);
    public Side Side => _settings.Side;
    public double BalanceQuantity => _balanceQuantity;

    public AdvancedRiskManagementModule(RiskManagementSettings settings)
    {
        _settings = settings;
        _rounding = settings.Side == Side.Long ? MidpointRounding.ToNegativeInfinity : MidpointRounding.ToPositiveInfinity;
        _currentStep = 0;
        _balanceQuantity = 0.0;
        _balanceAmount = 0.0;
        PlannedOrderPrice = settings.StartPrice;
        PlannedOrderQuantity = NextQuantity(_currentStep);

        // Инициализация экстремума и стоп-цены до открытия позиции.
        if (_settings.Side == Side.Long)
        {
            _maxPrice = settings.StartPrice;
            // Пока позиция не открыта, стоп равен StartPrice (можно добавить минимальный отступ, если нужно).
            StopPrice = Math.Round(settings.StartPrice, _settings.PriceRoundRatio, _rounding);
        }
        else
        {
            _minPrice = settings.StartPrice;
            StopPrice = Math.Round(settings.StartPrice, _settings.PriceRoundRatio, _rounding);
        }
        Status = RiskStatus.OrderRequest;
    }

    protected virtual double NextQuantity(int step)
    {
        return step == 0 ? _settings.OrderMultiplier : _settings.OrderMultiplier * Math.Pow(2, step);
    }

    /// <summary>
    /// Обновление стоп-цены на основе trailing-stop логики с расчетом безрисковой стоп-цены.
    /// Для Long: если позиция открыта и currentPrice > BalancePrice, то
    /// delta = currentPrice – BalancePrice, и безрисковая стоп-цена = currentPrice – (delta * _settings.LossRate).
    /// Если delta ≤ 0, стоп = BalancePrice.
    /// Для Short – аналогично с инверсией.
    /// </summary>
    private void UpdateTrailingStopPrice(double currentPrice)
    {
        // Если позиция ещё не открыта, оставляем StopPrice равным StartPrice.
        if (_balanceQuantity == 0)
        {
            StopPrice = Math.Round(_settings.StartPrice, _settings.PriceRoundRatio, _rounding);
            return;
        }

        if (_settings.Side == Side.Long)
        {
            double delta = currentPrice - BalancePrice;
            double candidateStop = delta > 0
                ? currentPrice - (delta * _settings.LossRate)
                : BalancePrice;
            // Если кандидат не изменился, применяем fallback trailing-stop с отступом 1/10 от LossRate.
            if (Math.Abs(candidateStop - BalancePrice) < 1e-8)
                candidateStop = currentPrice - (_settings.LossRate / 10);
            // Гарантируем, что стоп не опускается ниже BalancePrice.
            candidateStop = Math.Max(candidateStop, BalancePrice);
            StopPrice = Math.Round(candidateStop, _settings.PriceRoundRatio, _rounding);
        }
        else
        {
            double delta = BalancePrice - currentPrice;
            double candidateStop = delta > 0
                ? currentPrice + (delta * _settings.LossRate)
                : BalancePrice;
            if (Math.Abs(candidateStop - BalancePrice) < 1e-8)
                candidateStop = currentPrice + (_settings.LossRate / 10);
            candidateStop = Math.Min(candidateStop, BalancePrice);
            StopPrice = Math.Round(candidateStop, _settings.PriceRoundRatio, _rounding);
        }
    }

    private bool ShouldClosePosition(double currentPrice)
    {
        return _settings.Side == Side.Long ? currentPrice <= StopPrice : currentPrice >= StopPrice;
    }

    private double RecalculateUnrealizedPnl(double currentPrice)
    {
        if (_balanceQuantity == 0)
            return 0;
        double grossPnl = _settings.Side == Side.Long
            ? (currentPrice - BalancePrice) * _balanceQuantity * _settings.PointPrice
            : (BalancePrice - currentPrice) * _balanceQuantity * _settings.PointPrice;
        return Math.Round(grossPnl - FeeExecuted, 2, _rounding);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool CreateOrderRequest(double currentPrice)
    {
        UpdateTrailingStopPrice(currentPrice);
        CurrentUnrealizedPnl = RecalculateUnrealizedPnl(currentPrice);

        // Если позиция открыта и цена движется против неё, генерируем сигнал закрытия.
        if (_balanceQuantity > 0 && ShouldClosePosition(currentPrice))
        {
            Status = RiskStatus.CloseRequest;
            return true;
        }

        if (Status == RiskStatus.Finished)
            return false;

        // Если цена движется в нашу сторону для увеличения позиции.
        bool priceMovedInFavor = _settings.Side == Side.Long ? currentPrice >= PlannedOrderPrice : currentPrice <= PlannedOrderPrice;
        if (priceMovedInFavor)
        {
            double plannedWarrantyCoverage = (_balanceQuantity + PlannedOrderQuantity) * _settings.ContractMargin;
            if (plannedWarrantyCoverage > _settings.Balance)
            {
                Status = RiskStatus.Finished;
                return false;
            }
            _currentStep++;
            var (price, quantity) = NextLevel(_currentStep);
            PlannedOrderPrice = price;
            PlannedOrderQuantity = quantity;
            Status = RiskStatus.OrderRequest;
            return true;
        }

        Status = RiskStatus.Waiting;
        return false;
    }

    public void ExecutionReceived(UserExecution execution)
    {
        _balanceQuantity += execution.ExecutedQuantity;
        _balanceAmount += execution.ExecutionPrice * execution.ExecutedQuantity * _settings.PointPrice;
        // Если позиция только открылась, инициализируем базу.
        if (_balanceQuantity > 0)
        {
            // При первом исполнении устанавливаем базовую цену равной BalancePrice.
            // Последующие обновления trailing-stop будут основываться на текущей котировке.
        }
        UpdateTrailingStopPrice(execution.ExecutionPrice);
        CurrentUnrealizedPnl = RecalculateUnrealizedPnl(execution.ExecutionPrice);

        if (Status == RiskStatus.CloseRequest)
            Status = RiskStatus.Finished;
        else
            Status = RiskStatus.Waiting;
    }

    private (double Price, double Quantity) NextLevel(int step)
    {
        double multiplier = 1 + _settings.OrderIncreasePercentage * (int)_settings.Side;
        double nextPrice = Math.Round(_settings.StartPrice * Math.Pow(multiplier, step), _settings.PriceRoundRatio, _rounding);
        double quantity = NextQuantity(step);
        return (nextPrice, quantity);
    }
}
