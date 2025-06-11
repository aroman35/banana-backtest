using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.CryptoConverter.Options;

public class ConverterOptions
{
    public string? DownloadScheduleCrone { get; set; }
    public string? RefreshInstrumentsScheduleCrone { get; set; }
    public string? ReconciliationScheduleCrone { get; set; }
    public string? MetaBuildScheduleCrone { get; set; }
    public int Term { get; set; }
    public DateOnly HistoryDepth { get; set; }
    public int WorkersCount { get; set; }
    public int MaxDegreeOfParallelismForSchedulers { get; set; }
    public string ServerName { get; set; } = null!;
    public Exchange[] Exchanges { get; set; } = [];
    public string[] Queues { get; set; } = [];
}
