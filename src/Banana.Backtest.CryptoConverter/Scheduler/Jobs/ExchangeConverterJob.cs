using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.CryptoConverter.Options;
using Banana.Backtest.CryptoConverter.Services;
using Hangfire;
using Microsoft.Extensions.Options;

namespace Banana.Backtest.CryptoConverter.Scheduler.Jobs;

public class ExchangeConverterJob(
    CatalogRepository catalog,
    IBackgroundJobClient backgroundJobClient,
    IOptionsSnapshot<ConverterOptions> converterOptions)
{
    public async Task HandleAsync(RunAllInstrumentsHandlingCommand command, CancellationToken cancellationToken = default)
    {
        command.ValidateAndThrow();
        var endDate = command.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(converterOptions.Value.Term);
        var startDate = command.StartDate ?? converterOptions.Value.HistoryDepth;
        var datesCount = endDate.DayNumber - startDate.DayNumber;
        var daysRange = Enumerable.Range(0, datesCount + 1).Select(x => startDate.AddDays(x));

        var instruments = await catalog
            .GetInstruments(command.Exchange)
            .ToDictionaryAsync(x => x.Symbol, x => x, cancellationToken: cancellationToken);

        var allHashesBySymbol = instruments.Values
            .GroupJoin(
                daysRange,
                _ => true,
                _ => true,
                static (instrument, dates) => dates
                    .Where(date => date > instrument.AvailableSince)
                    .Select(date => MarketDataHash.Create(instrument.Symbol, date)))
            .SelectMany(hashes => hashes)
            .GroupBy(hash => hash.Symbol);

        foreach (var groupBySymbol in allHashesBySymbol)
        {
            var cataloguedHashes = await catalog
                .GetCompleteMetaForSymbol(groupBySymbol.Key)
                .ToHashSetAsync(cancellationToken: cancellationToken);

            var instrumentInfo = instruments[groupBySymbol.Key];
            foreach (var hash in groupBySymbol.Except(cataloguedHashes))
            {
                var levelUpdatesHash = hash.For(FeedType.LevelUpdates);
                var tradesHash = hash.For(FeedType.Trades);

                if (!cataloguedHashes.Contains(levelUpdatesHash))
                {
                    backgroundJobClient.Enqueue<MarketDataConverterJob<LevelUpdate>>(
                        HangfireDefaults.LEVEL_UPDATES_QUEUE,
                        converter => converter.HandleAsync(hash.For(FeedType.LevelUpdates), instrumentInfo, CancellationToken.None));
                }

                if (!cataloguedHashes.Contains(tradesHash))
                {
                    backgroundJobClient.Enqueue<MarketDataConverterJob<TradeUpdate>>(
                        HangfireDefaults.TRADES_QUEUE,
                        converter => converter.HandleAsync(hash.For(FeedType.Trades), instrumentInfo, CancellationToken.None));
                }
            }
        }
    }
}