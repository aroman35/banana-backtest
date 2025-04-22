using Banana.Strategies.MeanReverse.Binance.Execution.Models;
using Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;
using Banana.Strategies.MeanReverse.Binance.UserData.Positions;
using FastEndpoints;

namespace Banana.Strategies.MeanReverse.Binance.Endpoints.Positions.ClosePosition;

public class ClosePositionEndpoint(ICacheForSymbol<UserPosition> cache) : Endpoint<ClosePositionRequest, ExecutionResult>
{
    public override void Configure()
    {
        Post("/futures-position/close");
        AllowAnonymous();
    }

    public override async Task<ExecutionResult> ExecuteAsync(ClosePositionRequest request, CancellationToken cancellationToken)
    {
        var position = cache.Get(request.Symbol);
        var result = await position.CloseAsync(cancellationToken);
        return result;
    }
}
