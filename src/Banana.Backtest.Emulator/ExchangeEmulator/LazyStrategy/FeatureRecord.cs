namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy;

public class FeatureRecord
{
    public DateTime Timestamp { get; set; }

    // Признаки из стакана
    public double BestBid { get; set; }
    public double BestAsk { get; set; }
    public double Spread { get; set; }
    public double Imbalance { get; set; }
    public double VWAPBid { get; set; }
    public double VWAPAsk { get; set; }

    // Признаки из потока сделок
    public double TotalTradeVolume { get; set; }
    public int TradeCount { get; set; }
    public double BuyTradeVolume { get; set; }
    public double SellTradeVolume { get; set; }
    public double TradePriceChange { get; set; }
    public double MidPrice { get; set; }
    public double SpreadPct { get; set; }
    public double PriceChangePct { get; set; }
    public double TradeIntensity { get; set; }
    // Метка: 1 - точка смены тренда, 0 - нет
    public float Label { get; set; }
}
