using System.IO.Compression;
using System.Reflection;
using Banana.Backtest.Common.Models;
using Serilog;

namespace Banana.Backtest.Common.Services;

public class CacheDecompressor<TMarketDataType>(MarketDataHash hash, string sourcesDictionary, string destinationDictionary, ILogger logger) : IDisposable
    where TMarketDataType : unmanaged
{
    private static readonly FeedType Feed = typeof(TMarketDataType).GetCustomAttribute<FeedAttribute>()?.Feed
                                      ?? throw new ArgumentException(
                                          $"Feed type is not defined for {typeof(TMarketDataType).Name}. Ensure that {nameof(FeedAttribute)} is set.");
    private readonly IMarketDataCacheReader<TMarketDataType> _compressedSource = MarketDataCacheAccessorProvider.CreateReader<TMarketDataType>(sourcesDictionary, hash.For(Feed));
    private readonly IMarketDataCacheWriter<TMarketDataType> _destination = MarketDataCacheAccessorProvider.CreateWriter<TMarketDataType>(destinationDictionary, hash.For(Feed), CompressionType.NoCompression, CompressionLevel.NoCompression);
    private readonly ILogger _logger = logger.ForContext<CacheDecompressor<TMarketDataType>>();

    public void Start()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcesDictionary, nameof(sourcesDictionary));
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDictionary, nameof(destinationDictionary));
        if (sourcesDictionary == destinationDictionary)
            throw new InvalidOperationException("Source and destination dictionaries are the same");

        foreach (var item in _compressedSource.ContinueReadUntil())
        {
            _destination.Write(item);
        }
        _logger.Information("Cache preparation complete for {Hash}", hash);
    }

    public void Dispose()
    {
        _compressedSource.Dispose();
        _destination.Dispose();
    }
}
