using System.Runtime.CompilerServices;
using Banana.Strategies.MeanReverse.Binance.DataFlow;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Objects.Models.Futures.Socket;
using Microsoft.Extensions.Caching.Memory;

namespace Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;

public class UserBalanceCache(
    IMemoryCache memoryCache,
    IBinanceRestClient binanceRestClient,
    DataFlowMediator mediator,
    ILogger logger) : CacheForSymbol<BinanceFuturesStreamBalance>(memoryCache, logger)
{
    private readonly ILogger _logger = logger.ForContext<UserBalanceCache>();
    protected override async IAsyncEnumerable<KeyValuePair<string, BinanceFuturesStreamBalance>> LoadData([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = await binanceRestClient.UsdFuturesApi.Account.GetBalancesAsync(ct: cancellationToken);
        ThrowIfError(response.Error);
        foreach (var balance in response.Data)
        {
            var data = new BinanceFuturesStreamBalance
            {
                Asset = balance.Asset,
                WalletBalance = balance.WalletBalance,
                CrossWalletBalance = balance.CrossWalletBalance,
                BalanceChange = balance.AvailableBalance
            };
            yield return KeyValuePair.Create(balance.Asset, data);
        }
    }

    protected override Task UpdatesTask(CancellationToken cancellationToken)
    {
        var streamReader = HandleStreamData(
            mediator.Stream<BinanceFuturesStreamBalance>(cancellationToken),
            (balance, _) =>
            {
                Update(balance.Asset, balance);
                _logger.Debug("Balance updated for {Asset}: {Balance}", balance.Asset, balance.WalletBalance);
                return ValueTask.CompletedTask;
            }, cancellationToken: cancellationToken);
        return streamReader;
    }
}
