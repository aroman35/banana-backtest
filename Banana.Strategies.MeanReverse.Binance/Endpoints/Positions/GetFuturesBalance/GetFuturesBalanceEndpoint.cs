using Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;
using Binance.Net.Objects.Models.Futures.Socket;
using FastEndpoints;

namespace Banana.Strategies.MeanReverse.Binance.Endpoints.Positions.GetFuturesBalance;

public class GetFuturesBalanceEndpoint(ICacheForSymbol<BinanceFuturesStreamBalance> cache) : Endpoint<GetFuturesBalanceRequest, BinanceFuturesStreamBalance>
{
    public override void Configure()
    {
        Get("/futures-balance/{Asset}");
        AllowAnonymous();
    }

    public override Task<BinanceFuturesStreamBalance> ExecuteAsync(GetFuturesBalanceRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(cache.Get(request.Asset));
    }
}
