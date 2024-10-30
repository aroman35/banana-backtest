using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.CryptoConverter.Options;

public class ConverterOptions
{
    public string DownloadScheduleCrone { get; set; } = null!;
    public string RefreshInstrumentsScheduleCrone { get; set; } = null!;
    public int Term { get; set; }
    public DateOnly HistoryDepth { get; set; }
    public int WorkersCount { get; set; }
    public int MaxDegreeOfParallelismForSchedulers { get; set; }
    public string ServerName { get; set; } = null!;
    public Exchange[] Exchanges { get; set; } = [];
    public string[] Queues { get; set; } = [];
}