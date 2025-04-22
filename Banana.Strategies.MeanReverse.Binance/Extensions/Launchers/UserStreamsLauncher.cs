using Banana.Strategies.MeanReverse.Binance.DataFlow;
using Binance.Net.Interfaces.Clients;
using static Banana.Strategies.MeanReverse.Binance.Extensions.JobsExtensions;

namespace Banana.Strategies.MeanReverse.Binance.Extensions.Launchers;

public class UserStreamsLauncher(
    DataFlowMediator mediator,
    IBinanceRestClient binanceClient,
    IBinanceSocketClient binanceSocketClient,
    ILogger logger) : IHostedService
{
    private readonly ILogger _logger = logger.ForContext<UserStreamsLauncher>();
    private string? _listenKey;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var userStreamResponse = await binanceClient.UsdFuturesApi.Account.StartUserStreamAsync(cancellationToken);
        if (!userStreamResponse.Success)
        {
            _logger.Error("Cannot start user-streams ({Code}): {Error}", userStreamResponse.Error?.Code, userStreamResponse.Error?.Message);
            throw new InvalidOperationException(userStreamResponse.Error?.Message);
        }
        _listenKey = userStreamResponse.Data;

        await binanceSocketClient.UsdFuturesApi.Account.SubscribeToUserDataUpdatesAsync(
            _listenKey,
            onTradeUpdate: @event =>
            {
                SafeExecute(x => mediator.SendForSymbol(x, x.Symbol), @event.Data, _logger);
                SafeExecute(mediator.Send, @event.Data, _logger);
            },
            onOrderUpdate: @event =>
            {
                SafeExecute(x => mediator.SendForSymbol(x, x.UpdateData.Symbol), @event.Data, _logger);
                SafeExecute(mediator.Send, @event.Data, _logger);
            },
            onAccountUpdate: update =>
            {
                foreach (var @event in update.Data.UpdateData.Balances)
                {
                    SafeExecute(x => mediator.SendForSymbol(x, x.Asset), @event, _logger);
                    SafeExecute(mediator.Send, @event, _logger);
                }

                foreach (var @event in update.Data.UpdateData.Positions)
                {
                    SafeExecute(x => mediator.SendForSymbol(x, x.Symbol), @event, _logger);
                    SafeExecute(mediator.Send, @event, _logger);
                }
            },
            ct: cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_listenKey is not null)
            await binanceClient.UsdFuturesApi.Account.StopUserStreamAsync(_listenKey, cancellationToken);
    }
}
