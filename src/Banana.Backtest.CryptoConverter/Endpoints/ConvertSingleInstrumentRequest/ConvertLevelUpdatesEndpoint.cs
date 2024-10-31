using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.CryptoConverter.Scheduler;
using Banana.Backtest.CryptoConverter.Scheduler.Jobs;
using FastEndpoints;
using Hangfire;

namespace Banana.Backtest.CryptoConverter.Endpoints.ConvertSingleInstrumentRequest;

public class ConvertLevelUpdatesEndpoint(IBackgroundJobClient backgroundJobClient) : Endpoint<ConvertInstrumentCommand>
{
    public override void Configure()
    {
        Post("/convert/level-updates");
        AllowAnonymous();
    }

    public override Task HandleAsync(ConvertInstrumentCommand request, CancellationToken cancellationToken)
    {
        var hash = MarketDataHash.Create(request.Symbol, request.TradeDate, FeedType.LevelUpdates);
        backgroundJobClient.Enqueue<MarketDataConverterJob<LevelUpdate>>(
            HangfireDefaults.LEVEL_UPDATES_QUEUE,
            converter => converter.HandleAsync(hash, request.InstrumentInfo, CancellationToken.None));
        return Task.CompletedTask;
    }
}
