using FastEndpoints;
using Hangfire;

namespace Banana.Backtest.CryptoConverter.Endpoints.ClearBackgroundJobs;

public class ClearBackgroundJobsEndpoint(IBackgroundJobClientV2 jobClient, ILogger logger) : Endpoint<ClearBackgroundJobsRequest>
{
    private readonly ILogger _logger = logger.ForContext<ClearBackgroundJobsEndpoint>();
    public override void Configure()
    {
        Post("/scheduler/clear-queue");
        AllowAnonymous();
    }

    public override Task HandleAsync(ClearBackgroundJobsRequest request, CancellationToken cancellationToken)
    {
        var from = 0;
        var jobs = jobClient.Storage
            .GetMonitoringApi()
            .EnqueuedJobs(request.Queue, from, request.BatchSize)
            .Where(x => x.Value.State == "Enqueued")
            .ToList();
        while (jobs.Count != 0)
        {
            foreach (var (id, job) in jobs)
            {
                jobClient.Delete(id);
            }
            from += request.BatchSize;
            jobs = jobClient.Storage
                .GetMonitoringApi()
                .EnqueuedJobs(request.Queue, from, request.BatchSize)
                .Where(x => x.Value.State == "Enqueued")
                .ToList();
            _logger.Information("Deleted: {Count}", jobs.Count);
        }
        return Task.CompletedTask;
    }
}
