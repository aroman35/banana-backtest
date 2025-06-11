using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Options;
using Hangfire;
using Microsoft.Extensions.Options;

namespace Banana.Backtest.CryptoConverter.Scheduler.Jobs;

public class MetaBuildJobLauncher(
    IBackgroundJobClientV2 backgroundJobClient,
    IOptions<MarketDataParserOptions> marketDataParserOptions)
{
    public void Handle()
    {
        var directoryInfo = new DirectoryInfo(marketDataParserOptions.Value.OutputDirectory);
        var sourceFiles = directoryInfo.EnumerateFiles("*.dat", SearchOption.AllDirectories).ToList();
        sourceFiles
            .AsParallel()
            .WithDegreeOfParallelism(24)
            .ForAll(sourceFile =>
            {
                if (RegexExtensions.HashFromFileName(sourceFile.FullName, out var hash))
                {
                    if (hash.Feed is FeedType.Trades)
                    {
                        backgroundJobClient.Enqueue<MetaBuildJob<TradeUpdate>>(
                            HangfireDefaults.META_BUILD_QUEUE,
                            job => job.HandleAsync(hash));
                    }
                    else if (hash.Feed is FeedType.LevelUpdates)
                    {
                        backgroundJobClient.Enqueue<MetaBuildJob<LevelUpdate>>(
                            HangfireDefaults.META_BUILD_QUEUE,
                            job => job.HandleAsync(hash));
                    }
                }
            });
    }
}
