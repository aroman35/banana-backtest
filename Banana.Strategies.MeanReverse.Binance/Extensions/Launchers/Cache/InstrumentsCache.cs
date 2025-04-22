using System.Runtime.CompilerServices;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Objects.Models.Futures;
using Microsoft.Extensions.Caching.Memory;

namespace Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;

public class InstrumentsCache(
    IMemoryCache memoryCache,
    IBinanceRestClient binanceRestClient,
    ILogger logger) : CacheForSymbol<BinanceFuturesSymbol>(memoryCache, logger)
{
    protected override async IAsyncEnumerable<KeyValuePair<string, BinanceFuturesSymbol>> LoadData([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = await binanceRestClient.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync(cancellationToken);
        ThrowIfError(response.Error);
        foreach (var instrument in response.Data.Symbols)
        {
            yield return new KeyValuePair<string, BinanceFuturesSymbol>(instrument.Name, instrument);
        }
    }
}
