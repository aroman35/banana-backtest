using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.Common.Services;
using Banana.Backtest.Launcher.Cache.Catalog;

namespace Banana.Backtest.Launcher;

public class Test(InstrumentsCatalog instrumentsCatalog, ILogger logger) : IHostedService
{
    private const string CacheDirectory = "";
    private const string MarketDataDirectory = "D:/market-data-storage";
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var tradeDate = new DateOnly(2024, 09, 01);
        var endDate = new DateOnly(2024, 09, 30);
        for (; tradeDate <= endDate; tradeDate = tradeDate.AddDays(1))
        {
            var hash = await PrepareMarketDataForBaseAsset(tradeDate, "NG");
            var emulator = new Emulator.Emulator(hash, MarketDataDirectory, logger);
            emulator.Process();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task<MarketDataHash> PrepareMarketDataForBaseAsset(DateOnly dateOnly, string asset)
    {
        var instrument = await instrumentsCatalog.GetSymbolForAsset(dateOnly, Asset.Get(asset));
        var hash = MarketDataHash.Create(instrument, dateOnly);
        var levelUpdatesPath = hash.For(FeedType.LevelUpdates).FilePath(MarketDataDirectory);
        if (!File.Exists(levelUpdatesPath))
            ConvertOrdersToLevels(hash);
        return hash;
    }

    private void ConvertOrdersToLevels(MarketDataHash hash)
    {
        var settings = new LevelUpdatesConvertorSettings
        {
            StoragePath = MarketDataDirectory,
            OutputDirectoryPath = MarketDataDirectory,
            Hash = hash
        };
        using var converter = new LevelUpdatesConvertor(settings, logger);
        converter.Start();
    }

    private void MarketDataPreparationStep(string asset)
    {
        var ticker = Symbol.Create(Asset.Get(asset), Asset.USDT, Exchange.BinanceFutures);
        var startDate = new DateOnly(2024, 11, 01);
        var endDate = new DateOnly(2024, 11, 06);
        var sourcesDirectory = "W:/";
        var destinationDirectory = "E:/cache";
        for (var i = startDate; i < endDate; i = i.AddDays(1))
        {
            var hash = MarketDataHash.Create(ticker, i);
            using (var decompressor = new CacheDecompressor<LevelUpdate>(hash, sourcesDirectory, destinationDirectory, logger))
            {
                decompressor.Start();
            }
            using (var decompressor = new CacheDecompressor<TradeUpdate>(hash, sourcesDirectory, destinationDirectory, logger))
            {
                decompressor.Start();
            }
        }
    }
}
