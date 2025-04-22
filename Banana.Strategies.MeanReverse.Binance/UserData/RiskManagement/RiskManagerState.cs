using Banana.Backtest.Common.Models;
using Banana.Strategies.MeanReverse.Binance.Extensions;
using Banana.Strategies.MeanReverse.Binance.UserData.Positions;
using Binance.Net.Objects.Models.Futures;

namespace Banana.Strategies.MeanReverse.Binance.UserData.RiskManagement;

public class RiskManagerState
{
    public RiskManagerState(
        BinanceFuturesSymbol instrument,
        UserPosition position,
        CreateRiskManagementCommand command)
    {
        Symbol = instrument.Name;
        Position = position;
        Instrument = instrument;
        LastExecutionPrice = command.InitialPrice;
        ExtremumPrice = command.InitialPrice;
        StopLossPercent = command.StopLossPercent;
        IncreasePositionPercent = command.IncreasePositionPercent;
        Id = Guid.NewGuid();
        ConnectedExecutionId = command.ConnectedExecutionId;
    }

    public Guid Id { get; private set; }
    public string Symbol { get; private set; }
    public decimal LastExecutionPrice { get; private set; }
    public decimal StopLossPrice { get; private set; }
    public decimal ExtremumPrice { get; private set; }
    public decimal CurrentPrice { get; private set; }
    public decimal StopLossPercent { get; private set; }
    public decimal IncreasePositionPercent { get; private set; }
    public BinanceFuturesSymbol Instrument { get; private set; }
    public UserPosition Position { get; private set; }
    public Guid? ConnectedExecutionId { get; private set; }
    public Side Side => Position.State.Side;
    public int SideMultiplexer => (int)Side;
    public bool IsStopLossTriggered => (StopLossPrice - CurrentPrice) * SideMultiplexer >= 0;

    public void PriceUpdated(decimal price)
    {
        CurrentPrice = price;
        if ((CurrentPrice - ExtremumPrice) * SideMultiplexer > 0)
        {
            ExtremumPrice = CurrentPrice;
        }

        StopLossPrice = Instrument.RoundPrice(
            ExtremumPrice - ExtremumPrice * StopLossPercent * SideMultiplexer,
            Side);
    }
}
