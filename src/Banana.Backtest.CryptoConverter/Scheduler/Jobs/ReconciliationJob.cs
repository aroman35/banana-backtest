using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Options;
using Banana.Backtest.CryptoConverter.Options;
using Hangfire;
using Microsoft.Extensions.Options;

namespace Banana.Backtest.CryptoConverter.Scheduler.Jobs;

public class ReconciliationJob(
    IBackgroundJobClientV2 backgroundJobClient,
    IOptions<ConverterOptions> converterOptions,
    IOptions<MarketDataParserOptions> marketDataParserOptions)
{
    public void Handle()
    {
        var directoryInfo = new DirectoryInfo(marketDataParserOptions.Value.OutputDirectory);
        var sourceFiles = directoryInfo
            .EnumerateFiles("*.dat", SearchOption.AllDirectories)
            .Where(file => file.Length == 0)
            .ToList();

        sourceFiles
            .AsParallel()
            .WithDegreeOfParallelism(24)
            .ForAll(sourceFile =>
            {
                if (RegexExtensions.HashFromFileName(sourceFile.FullName, out var hash))
                {
                    if (!converterOptions.Value.Exchanges.Contains(hash.Symbol.Exchange))
                        return;
                    if (hash.Feed is FeedType.Trades)
                    {
                        backgroundJobClient.Enqueue<MarketDataConverterJob<TradeUpdate>>(
                            HangfireDefaults.TRADES_QUEUE,
                            job => job.HandleAsync(hash, CancellationToken.None));
                    }
                    else if (hash.Feed is FeedType.LevelUpdates)
                    {
                        backgroundJobClient.Enqueue<MarketDataConverterJob<LevelUpdate>>(
                            HangfireDefaults.LEVEL_UPDATES_QUEUE,
                            job => job.HandleAsync(hash, CancellationToken.None));
                    }
                }
            });
    }
}
