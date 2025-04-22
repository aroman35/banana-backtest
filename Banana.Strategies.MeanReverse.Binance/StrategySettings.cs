namespace Banana.Strategies.MeanReverse.Binance;

public class StrategySettings
{
    public double LowerBoundVolumeQuantile { get; set; }
    public double UpperBoundVolumeQuantile { get; set; }
    public double LowerBoundVolatilityQuantile { get; set; }
    public double UpperBoundVolatilityQuantile { get; set; }
    public double ReversalThresholdPercent { get; set; }
    public bool ReverseTrend { get; set; }
    public int MinOrderMultiplier { get; set; }
    public bool IsMakerOnly { get; set; }
    public TimeSpan OrderTimeout { get; set; }
    public int IcebergDivider { get; set; }
    public decimal StopLossPercent { get; set; }
}
