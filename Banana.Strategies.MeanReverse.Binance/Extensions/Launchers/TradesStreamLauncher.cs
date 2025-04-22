using Banana.Strategies.MeanReverse.Binance.DataFlow;
using Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Objects.Models.Futures;
using Microsoft.Extensions.Options;

namespace Banana.Strategies.MeanReverse.Binance.Extensions.Launchers;

public class TradesStreamLauncher(
    ICacheForSymbol<BinanceFuturesSymbol> instrumentsCache,
    IBinanceSocketClient binanceSocketClient,
    DataFlowMediator mediator,
    IOptions<RuntimeSettings> runtimeSettings,
    ILogger logger) : IHostedService
{
    private readonly ILogger _logger = logger.ForContext<TradesStreamLauncher>();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await instrumentsCache.WaitForInitialisation;
        var symbols = runtimeSettings.Value.LimitedSymbols ?? instrumentsCache.Symbols;
        foreach (var symbolsToDeploy in symbols.Chunk(10))
        {
            var subscription = await binanceSocketClient.UsdFuturesApi.ExchangeData.SubscribeToTradeUpdatesAsync(
                symbolsToDeploy,
                message => SafeExecute(x => mediator.SendForSymbol(x.Data, x.Data.Symbol), message, _logger),
                ct: cancellationToken);
            if (!subscription.Success)
            {
                _logger.Error(
                    "Failed to Subscribe to trades stream ({Code}): {Message}",
                    subscription.Error?.Code,
                    subscription.Error?.Message);
            }
            subscription.Data.ConnectionLost += () => _logger.Error("Trades stream connection lost");
            subscription.Data.ConnectionClosed += () => _logger.Error("Trades stream connection closed");
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
        _logger.Information("Trades stream subscribed");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
