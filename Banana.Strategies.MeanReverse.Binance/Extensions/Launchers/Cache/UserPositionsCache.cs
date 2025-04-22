using System.Runtime.CompilerServices;
using Banana.Strategies.MeanReverse.Binance.DataFlow;
using Banana.Strategies.MeanReverse.Binance.Execution;
using Banana.Strategies.MeanReverse.Binance.UserData.Positions;
using Binance.Net.Interfaces.Clients;
using Microsoft.Extensions.Caching.Memory;

namespace Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;

public class UserPositionsCache(
    IMemoryCache memoryCache,
    IBinanceRestClient binanceRestClient,
    DeferredExecution deferredExecution,
    DataFlowMediator mediator,
    ILogger logger) : CacheForSymbol<UserPosition>(memoryCache, logger: logger)
{
    private readonly ILogger _logger = logger;
    protected override async IAsyncEnumerable<KeyValuePair<string, UserPosition>> LoadData([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = await binanceRestClient.UsdFuturesApi.Account.GetPositionInformationAsync(ct: cancellationToken);
        ThrowIfError(response.Error);
        foreach (var binancePositionDetails in response.Data)
        {
            var userPosition = new UserPosition(binancePositionDetails, deferredExecution, mediator, cancellationToken, _logger);
            yield return new KeyValuePair<string, UserPosition>(binancePositionDetails.Symbol, userPosition);
        }
    }
}
