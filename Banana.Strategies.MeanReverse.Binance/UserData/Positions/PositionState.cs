using Banana.Backtest.Common.Models;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures;
using Binance.Net.Objects.Models.Futures.Socket;

namespace Banana.Strategies.MeanReverse.Binance.UserData.Positions;

public class PositionState
{
    public PositionState(BinancePositionDetailsUsdt positionDetails)
    {
        Symbol = positionDetails.Symbol;
        MaxNotional = positionDetails.MaxNotional;
        Notional = positionDetails.Notional;
        IsolatedWallet = positionDetails.IsolatedWallet;
        MarginType = positionDetails.MarginType;
        IsAutoAddMargin = positionDetails.IsAutoAddMargin;
        IsolatedMargin = positionDetails.IsolatedMargin;
        LiquidationPrice = positionDetails.LiquidationPrice;
        Quantity = Math.Abs(positionDetails.Quantity);
        UpdateTime = positionDetails.UpdateTime;
        Side = (Side)Math.Sign(Quantity);
        EntryPrice = positionDetails.EntryPrice;
        UnrealizedPnl = positionDetails.UnrealizedPnl;
    }

    public string Symbol { get; private set; }

    public decimal MaxNotional { get; private set; }

    public decimal Notional { get; private set; }

    public decimal IsolatedWallet { get; private set; }

    public FuturesMarginType MarginType { get; private set; }

    public bool IsAutoAddMargin { get; private set; }

    public decimal IsolatedMargin { get; private set; }

    public decimal LiquidationPrice { get; private set; }

    public decimal Quantity { get; private set; }

    public DateTime UpdateTime { get; private set; }

    public Side Side { get; private set; }

    public decimal EntryPrice { get; private set; }

    public decimal RealizedPnl { get; private set; }

    public decimal UnrealizedPnl { get; private set; }

    public void Update(BinanceFuturesStreamPosition update)
    {
        Quantity = Math.Abs(update.Quantity);
        EntryPrice = update.EntryPrice;
        UnrealizedPnl = update.UnrealizedPnl;
        RealizedPnl = update.RealizedPnl;
        MarginType = update.MarginType;
        IsolatedMargin = update.IsolatedMargin;
        Side = (Side)Math.Sign(update.Quantity);
    }
}
