using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Options;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.Common.Services;
using Microsoft.Extensions.Options;

namespace Banana.Backtest.CryptoConverter.Parsers;

public class TardisParserHandler<TMarketDataType>(
    IOptions<MarketDataParserOptions> options,
    MarketDataHash hash) : IParserHandler<TMarketDataType>
    where TMarketDataType : unmanaged
{
    private readonly IMarketDataCacheWriter<TMarketDataType> _marketDataCacheWriter =
        MarketDataCacheAccessorProvider.CreateWriter<TMarketDataType>(
            options.Value.OutputDirectory,
            hash,
            options.Value.CompressionType,
            options.Value.CompressionLevel
        );
    public void Handle(MarketDataItem<TMarketDataType> marketDataItem, Symbol ticker)
    {
        _marketDataCacheWriter.Write(marketDataItem);
    }

    public void Dispose()
    {
        _marketDataCacheWriter.Dispose();
    }
}
