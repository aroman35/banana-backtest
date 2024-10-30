using System.IO.Compression;
using System.Reflection;
using Banana.Backtest.Common.Models;
using Banana.Backtest.CryptoConverter.Parsers;
using Banana.Backtest.CryptoConverter.Services;
using Banana.Backtest.CryptoConverter.Services.Models.Tardis;

namespace Banana.Backtest.CryptoConverter.Scheduler.Jobs;

public class MarketDataConverterJob<TMarketDataType>(
    TardisClient tardisClient,
    ParsersProvider parsersProvider,
    CatalogRepository catalog)
    where TMarketDataType : unmanaged
{
    private readonly FeedType _feedType = typeof(TMarketDataType).GetCustomAttribute<FeedAttribute>()?.Feed
                                          ?? throw new ArgumentException(
                                              $"Feed type is not defined for {typeof(TMarketDataType).Name}. Ensure that {nameof(FeedAttribute)} is set.");

    public async Task HandleAsync(MarketDataHash hash, InstrumentInfo instrumentInfo, CancellationToken cancellationToken = default)
    {
        if (hash.Feed != _feedType)
            throw new ArgumentException($"Invalid hash feed type: {hash.Feed}");

        await using var tardisStream = await tardisClient.DownloadDatasetFileAsync(hash, instrumentInfo, cancellationToken);
        await using (var decompressionStream = new GZipStream(tardisStream, CompressionMode.Decompress))
        {
            parsersProvider.ParseTardisSource<TMarketDataType>(decompressionStream, hash);
        }
        await catalog.BuildComplete(hash);
    }
}