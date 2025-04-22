using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.CryptoConverter.Endpoints.EnqueueBackgroundTask;
using Banana.Backtest.CryptoConverter.Scheduler;
using Banana.Backtest.CryptoConverter.Scheduler.Jobs;
using FastEndpoints;
using Hangfire;

namespace Banana.Backtest.CryptoConverter.Endpoints.EnqueueSingleSymbolTask;

public class EnqueueSingleSymbolTaskEndpoint(IBackgroundJobClient backgroundJobClient) : Endpoint<EnqueueSingleSymbolTaskCommand, EnqueueBackgroundTaskResponse>
{
    public override void Configure()
    {
        Post("/schedule-single-symbol");
        AllowAnonymous();
    }

    public override Task<EnqueueBackgroundTaskResponse> ExecuteAsync(EnqueueSingleSymbolTaskCommand request, CancellationToken cancellationToken)
    {
        var response = new EnqueueBackgroundTaskResponse();
        try
        {
            var startDate = request.StartDate;
            for (; startDate <= request.EndDate; startDate = startDate.AddDays(1))
            {
                var hash = MarketDataHash.Create(request.Symbol, startDate);

                var levelUpdatesJobId = backgroundJobClient.Enqueue<MarketDataConverterJob<LevelUpdate>>(
                    HangfireDefaults.LEVEL_UPDATES_QUEUE,
                    converter => converter.HandleAsync(hash.For(FeedType.LevelUpdates), CancellationToken.None));
                response.CreatedJobs.Add(levelUpdatesJobId);

                var tradesJobId = backgroundJobClient.Enqueue<MarketDataConverterJob<TradeUpdate>>(
                    HangfireDefaults.TRADES_QUEUE,
                    converter => converter.HandleAsync(hash.For(FeedType.Trades), CancellationToken.None));
                response.CreatedJobs.Add(tradesJobId);
            }
        }
        catch (Exception exception)
        {
            response.Error = exception.Message;
        }

        response.CreatedJobsCount = response.CreatedJobs.Count;
        return Task.FromResult(response);
    }
}
