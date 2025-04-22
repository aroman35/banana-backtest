namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy.Runtime;

public class ModelInput
{
    public float BestBid { get; set; }
    public float BestAsk { get; set; }
    public float Spread { get; set; }
    public float Imbalance { get; set; }
    public float VWAPBid { get; set; }
    public float VWAPAsk { get; set; }
    public float TotalTradeVolume { get; set; }
    public float TradeCount { get; set; }
    public float BuyTradeVolume { get; set; }
    public float SellTradeVolume { get; set; }
    public float TradePriceChange { get; set; }
    public float MidPrice { get; set; }
    public float SpreadPct { get; set; }
    public float PriceChangePct { get; set; }
    public float TradeIntensity { get; set; }

    public bool IsValid()
    {
        return BestBid >= 0
               && BestAsk >= 0
               && Spread >= 0
               && Imbalance >= 0
               && VWAPBid >= 0
               && VWAPAsk > 0
               && TotalTradeVolume >= 0
               && TradeCount >= 0
               && BuyTradeVolume >= 0
               && SellTradeVolume >= 0
               && TradePriceChange >= 0
               && MidPrice >= 0
               && SpreadPct >= 0
               && PriceChangePct >= 0
               && TradeIntensity >= 0;
    }
}
