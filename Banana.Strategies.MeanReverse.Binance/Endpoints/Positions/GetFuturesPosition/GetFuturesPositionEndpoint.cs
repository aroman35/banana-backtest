using Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;
using Banana.Strategies.MeanReverse.Binance.UserData.Positions;
using FastEndpoints;

namespace Banana.Strategies.MeanReverse.Binance.Endpoints.Positions.GetFuturesPosition;

public class GetFuturesPositionEndpoint(ICacheForSymbol<UserPosition> cache) : Endpoint<GetFuturesPositionRequest, PositionState?>
{
    public override void Configure()
    {
        Get("/futures-position/{Symbol}");
        AllowAnonymous();
    }

    public override Task<PositionState?> ExecuteAsync(GetFuturesPositionRequest request, CancellationToken cancellationToken)
    {
        var position = cache.Get(request.Symbol);
        return Task.FromResult(position?.State);
    }
}
