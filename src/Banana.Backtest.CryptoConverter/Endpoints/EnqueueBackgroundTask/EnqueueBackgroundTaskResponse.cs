namespace Banana.Backtest.CryptoConverter.Endpoints.EnqueueBackgroundTask;

public class EnqueueBackgroundTaskResponse
{
    public int CreatedJobsCount { get; set; }
    public string? Error { get; set; }
    public ICollection<string> CreatedJobs { get; set; } = new HashSet<string>();
}