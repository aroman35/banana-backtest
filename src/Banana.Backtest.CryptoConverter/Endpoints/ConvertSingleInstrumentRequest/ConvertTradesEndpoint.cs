using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.CryptoConverter.Scheduler;
using Banana.Backtest.CryptoConverter.Scheduler.Jobs;
using FastEndpoints;
using Hangfire;

namespace Banana.Backtest.CryptoConverter.Endpoints.ConvertSingleInstrumentRequest;

public class ConvertTradesEndpoint(IBackgroundJobClient backgroundJobClient) : Endpoint<ConvertInstrumentCommand>
{
    public override void Configure()
    {
        Post("/convert/trades");
        AllowAnonymous();
    }

    public override Task HandleAsync(ConvertInstrumentCommand request, CancellationToken cancellationToken)
    {
        var hash = MarketDataHash.Create(request.Symbol, request.TradeDate, FeedType.Trades);
        backgroundJobClient.Enqueue<MarketDataConverterJob<TradeUpdate>>(
            HangfireDefaults.TRADES_QUEUE,
            converter => converter.HandleAsync(hash, CancellationToken.None));
        return Task.CompletedTask;
    }
}