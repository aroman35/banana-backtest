using Banana.Strategies.MeanReverse.Binance.Extensions;
using Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;
using Binance.Net.Objects.Models.Futures;
using CryptoExchange.Net.Interfaces;
using FastEndpoints;
using Microsoft.Extensions.Caching.Memory;

namespace Banana.Strategies.MeanReverse.Binance.Endpoints.Positions.CalculateMinOrderSize;

public class CalculateMinOrderSizeEndpoint(
    IMemoryCache memoryCache,
    ICacheForSymbol<BinanceFuturesSymbol> instrumentsCache) : Endpoint<CalculateMinOrderSizeRequest, decimal>
{
    public override void Configure()
    {
        Get("/futures-min-order-size/{Symbol}");
        AllowAnonymous();
    }

    public override Task<decimal> ExecuteAsync(CalculateMinOrderSizeRequest request, CancellationToken cancellationToken)
    {
        var orderBook = memoryCache.GetForSymbol<ISymbolOrderBook>(request.Symbol);
        var instrument = instrumentsCache.Get(request.Symbol);
        var midPrice = (orderBook.BestAsk.Price + orderBook.BestBid.Price) / 2;
        return Task.FromResult(instrument.MinOrderQuantity(midPrice));
    }
}
