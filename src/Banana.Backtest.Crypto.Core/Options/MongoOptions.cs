namespace Banana.Backtest.Crypto.Core.Options;

public class MongoOptions
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public string Prefix { get; set; } = null!;
    public string BackupPostfix { get; set; } = null!;
}
