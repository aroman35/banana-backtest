using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.Common.Services;

namespace Banana.Backtest.Launcher;

public class Test(ILogger logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var asset = "ETH";
        // MarketDataPreparationStep(asset);
        var ticker = Symbol.Create(Asset.Get(asset), Asset.USDT, Exchange.BinanceFutures);
        var startDate = new DateOnly(2024, 11, 02);
        var destinationDirectory = "E:/cache";
        var hash = MarketDataHash.Create(ticker, startDate);
        var levelsReader = MarketDataCacheAccessorProvider.CreateReader<LevelUpdate>(destinationDirectory, hash, true);
        var tradesReader = MarketDataCacheAccessorProvider.CreateReader<TradeUpdate>(destinationDirectory, hash, true);
        // var fullLevels = levelsReader.ContinueReadUntil().ToArray();
        // var fullTrades = tradesReader.ContinueReadUntil().ToArray();

        var emulator = new Emulator.Emulator(hash, destinationDirectory, logger);
        emulator.Process();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
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
