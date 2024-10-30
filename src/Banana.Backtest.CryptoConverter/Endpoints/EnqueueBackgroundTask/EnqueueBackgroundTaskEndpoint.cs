using Banana.Backtest.CryptoConverter.Scheduler;
using Banana.Backtest.CryptoConverter.Scheduler.Jobs;
using FastEndpoints;
using Hangfire;

namespace Banana.Backtest.CryptoConverter.Endpoints.EnqueueBackgroundTask;

public class EnqueueBackgroundTaskEndpoint(IBackgroundJobClient backgroundJobClient)
    : Endpoint<EnqueueBackgroundTaskCommand>
{
    public override void Configure()
    {
        Post("/schedule/{Exchange}");
        AllowAnonymous();
    }

    public override Task HandleAsync(EnqueueBackgroundTaskCommand request, CancellationToken cancellationToken)
    {
        if (request.Shift.HasValue)
        {
            backgroundJobClient.Schedule<ExchangeConverterJob>(
                service => service.HandleAsync(Map(request), CancellationToken.None),
                request.Shift.Value);
        }
        else
        {
            backgroundJobClient.Enqueue<ExchangeConverterJob>(
                service => service.HandleAsync(Map(request), CancellationToken.None));
        }
        return Task.CompletedTask;
    }

    private RunAllInstrumentsHandlingCommand Map(EnqueueBackgroundTaskCommand request)
    {
        return new RunAllInstrumentsHandlingCommand(request.StartDate, request.EndDate, request.Exchange);
    }
}