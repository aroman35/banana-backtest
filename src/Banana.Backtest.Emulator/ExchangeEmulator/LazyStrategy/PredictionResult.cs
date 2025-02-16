namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy;

public class PredictionResult
{
    public bool PredictedLabel { get; set; }
    public float Score { get; set; }
    public bool Label { get; set; }
}
