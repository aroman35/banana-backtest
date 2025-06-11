using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.CryptoConverter.Services;

namespace Banana.Backtest.CryptoConverter.Scheduler.Jobs;

public class RefreshInstrumentsJob(TardisClient tardisClient, CatalogRepository catalogRepository, ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<RefreshInstrumentsJob>();

    public async Task HandleAsync(Exchange exchange, CancellationToken cancellationToken)
    {
        var instruments = await tardisClient
            .GetExchangeInstrumentsAsync(exchange, cancellationToken)
            .ToArrayAsync(cancellationToken: cancellationToken);

        await catalogRepository.UpdateInstruments(instruments);
        _logger.Information("{Count} instruments refreshed for {Exchange}", instruments.Length, exchange);
    }
}
