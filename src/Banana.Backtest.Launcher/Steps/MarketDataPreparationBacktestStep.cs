using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.Common.Services;
using Banana.Backtest.Launcher.Options;
using Banana.Backtest.Launcher.Steps.Common;
using Microsoft.Extensions.Options;
// ReSharper disable ConvertToUsingDeclaration

namespace Banana.Backtest.Launcher.Steps;

public class MarketDataPreparationBacktestStep(
    IOptions<StrategyOptions> strategyOptions,
    IOptions<MarketDataSourcesOptions> marketDataSourcesOptions,
    ILogger logger) : IBacktestStep
{
    private readonly ILogger _logger = logger.ForContext<MarketDataPreparationBacktestStep>();
    public int Priority => 1;
    public bool IsBackground => false;

    public Task WaitForCompletion(CancellationToken cancellationToken = default)
    {
        var hash = MarketDataHash.Create(Symbol.Parse(strategyOptions.Value.Symbol), strategyOptions.Value.TradeDate);
        PrepareCache<TradeUpdate>(hash.For(FeedType.Trades));
        PrepareCache<LevelUpdate>(hash.For(FeedType.LevelUpdates));
        return Task.CompletedTask;
    }

    private void PrepareCache<TMarketDataType>(MarketDataHash hash)
        where TMarketDataType : unmanaged
    {
        var localCache = hash.FilePath(marketDataSourcesOptions.Value.CacheDirectory);

        if (File.Exists(localCache))
        {
            _logger.Debug("Market data cache exists at: {CachePath}", localCache);
            return;
        }

        var sourceFilePath = hash.FilePath(marketDataSourcesOptions.Value.MarketDataDirectory);
        if (!File.Exists(sourceFilePath))
        {
            _logger.Error("Market data source file doesn't exist at {SourceFilePath}", sourceFilePath);
            throw new FileNotFoundException($"Source market data file not found at {sourceFilePath}");
        }

        using (var decompressor = new CacheDecompressor<TMarketDataType>(
                   hash,
                   marketDataSourcesOptions.Value.MarketDataDirectory,
                   marketDataSourcesOptions.Value.CacheDirectory,
                   logger))
        {
            _logger.Debug("Building market data cache to local path: {LocalPath}", localCache);
            decompressor.Start();
        }
    }
}
