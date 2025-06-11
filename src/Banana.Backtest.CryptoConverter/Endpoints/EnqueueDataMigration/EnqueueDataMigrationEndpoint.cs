using Banana.Backtest.CryptoConverter.Scheduler.Jobs;
using FastEndpoints;

namespace Banana.Backtest.CryptoConverter.Endpoints.EnqueueDataMigration;

public class EnqueueDataMigrationEndpoint(DataCopierJobLauncher dataCopierJobLauncher) : Endpoint<EnqueueDataMigrationRequest>
{
    public override void Configure()
    {
        Post("/migration/launch");
        AllowAnonymous();
    }

    public override Task HandleAsync(EnqueueDataMigrationRequest request, CancellationToken cancellationToken)
    {
        dataCopierJobLauncher.Handle(request.SourceDirectory);
        return Task.CompletedTask;
    }
}
