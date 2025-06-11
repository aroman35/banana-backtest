using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.Options;
using Banana.Backtest.Common.Services;
using Banana.Backtest.CryptoConverter.Parsers;
using Banana.Backtest.CryptoConverter.Services;
using Microsoft.Extensions.Options;

namespace Banana.Backtest.CryptoConverter.Scheduler.Jobs;

public class MarketDataConverterJob<TMarketDataType>(
    TardisClient tardisClient,
    ParsersProvider parsersProvider,
    CatalogRepository catalogRepository,
    IOptions<MarketDataParserOptions> options,
    ILogger logger)
    where TMarketDataType : unmanaged
{
    private readonly ILogger _logger = logger.ForContext<MarketDataConverterJob<TMarketDataType>>();
    private readonly FeedType _feedType = typeof(TMarketDataType).GetCustomAttribute<FeedAttribute>()?.Feed
                                          ?? throw new ArgumentException(
                                              $"Feed type is not defined for {typeof(TMarketDataType).Name}. Ensure that {nameof(FeedAttribute)} is set.");

    public async Task HandleAsync(MarketDataHash hash, CancellationToken cancellationToken = default)
    {
        if (hash.Feed != _feedType)
            throw new ArgumentException($"Invalid hash feed type: {hash.Feed}");

        try
        {
            var startedAt = Stopwatch.GetTimestamp();
            var instrumentInfo = await catalogRepository.GetInstrument(hash.Symbol);
            ArgumentNullException.ThrowIfNull(instrumentInfo);
            _logger.Information("Starting for {Hash}: {Type}", hash, Helpers.FriendlyTypeName<TMarketDataType>());
            await using var tardisStream = await tardisClient.DownloadDatasetFileAsync(hash, instrumentInfo, cancellationToken);
            await using (var decompressionStream = new GZipStream(tardisStream, CompressionMode.Decompress))
            {
                parsersProvider.ParseTardisSource<TMarketDataType>(decompressionStream, hash);
            }

            var meta = MarketDataCacheAccessorProvider.ReadMeta<TMarketDataType>(options.Value.OutputDirectory, hash);
            await catalogRepository.BuildComplete(meta);
            var timeElapsed = Stopwatch.GetElapsedTime(startedAt);
            _logger.Information(
                "Market data job complete for {Hash}: {MarketData} in {Elapsed}",
                hash,
                Helpers.FriendlyTypeName<TMarketDataType>(),
                timeElapsed);
        }
        catch (Exception exception)
        {
            var file = hash.FilePath(options.Value.OutputDirectory);
            if (File.Exists(file))
                File.Delete(file);
            _logger.Error(exception, "Error while processing {Hash}", hash);
            throw;
        }
    }
}
