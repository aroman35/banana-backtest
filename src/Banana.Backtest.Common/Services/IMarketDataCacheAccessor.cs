using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;

namespace Banana.Backtest.Common.Services;

public interface IMarketDataCacheAccessor : IDisposable
{
    bool IsEmpty { get; }
    long ItemsCount { get; }
    MarketDataHash Hash { get; }
}

public interface IMarketDataCacheWriter<TMarketDataType> : IMarketDataCacheAccessor
    where TMarketDataType : unmanaged
{
    void Write(MarketDataItem<TMarketDataType> marketDataItem);
}

public interface IMarketDataCacheReader<TMarketDataType> : IMarketDataCacheAccessor
    where TMarketDataType : unmanaged
{
    IEnumerable<MarketDataItem<TMarketDataType>> ContinueReadUntil(long? timestamp = null);
    void ResetReader();
}