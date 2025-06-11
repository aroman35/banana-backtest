namespace Banana.Backtest.CryptoConverter.Endpoints.ClearBackgroundJobs;

public class ClearBackgroundJobsRequest
{
    public int BatchSize { get; set; }
    public required string Queue { get; set; }
}
