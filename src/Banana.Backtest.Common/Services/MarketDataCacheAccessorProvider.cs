using System.IO.Compression;
using Banana.Backtest.Common.Models;

namespace Banana.Backtest.Common.Services;

public static class MarketDataCacheAccessorProvider
{
    public static IMarketDataCacheReader<TMarketDataType> CreateReader<TMarketDataType>(
        string? sourcesDirectory,
        MarketDataHash hash)
        where TMarketDataType : unmanaged
    {
        return new MarketDataCacheAccessor<TMarketDataType>(sourcesDirectory, hash);
    }

    public static IMarketDataCacheWriter<TMarketDataType> CreateWriter<TMarketDataType>(
        string? sourcesDirectory,
        MarketDataHash hash,
        CompressionType compressionType,
        CompressionLevel compressionLevel)
        where TMarketDataType : unmanaged
    {
        return new MarketDataCacheAccessor<TMarketDataType>(sourcesDirectory, hash, compressionType, compressionLevel);
    }
}