namespace Banana.Backtest.Launcher.Options;

public class MarketDataSourcesOptions
{
    public string MarketDataDirectory { get; set; } = null!;
    public string CacheDirectory { get; set; } = null!;
    public bool UseMmf { get; set; }
}
