namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy;

public class FeatureRecordMapped
{
    public DateTime Timestamp { get; set; }
    public float BestBid { get; set; }
    public float BestAsk { get; set; }
    public float Spread { get; set; }
    public float Imbalance { get; set; }
    public float VWAPBid { get; set; }
    public float VWAPAsk { get; set; }
    public float TotalTradeVolume { get; set; }
    public float TradeCount { get; set; } // Теперь float, а не int
    public float BuyTradeVolume { get; set; }
    public float SellTradeVolume { get; set; }
    public float TradePriceChange { get; set; }
    public float MidPrice { get; set; }
    public float SpreadPct { get; set; }
    public float PriceChangePct { get; set; }
    public float TradeIntensity { get; set; }
    public bool Label { get; set; }
}
