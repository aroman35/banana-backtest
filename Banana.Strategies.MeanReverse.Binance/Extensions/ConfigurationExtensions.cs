using System.Threading.Channels;
using Banana.Strategies.MeanReverse.Binance.Execution.Models;
using Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;
using Banana.Strategies.MeanReverse.Binance.MarketData;
using Banana.Strategies.MeanReverse.Binance.UserData.Positions;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Objects.Models.Futures.Socket;
using Binance.Net.Objects.Models.Spot.Socket;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Banana.Strategies.MeanReverse.Binance.Extensions;

public static class ConfigurationExtensions
{
    public static async Task AddChannels(this IServiceCollection services)
    {
        var provider = services.BuildServiceProvider();
        var binanceClient = provider.GetRequiredService<IBinanceRestClient>();
        var exchangeInfo = await binanceClient.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();

        foreach (var instrument in exchangeInfo.Data.Symbols)
        {
            var symbol = instrument.Name;

            services.AddChannelForSymbol<OrderBookTop>(symbol);
            services.AddChannelForSymbol<BinanceStreamTrade>(symbol);
            services.AddChannelForSymbol<BinanceFuturesStreamTradeUpdate>(symbol);
            services.AddChannelForSymbol<BinanceFuturesStreamOrderUpdate>(symbol);
            services.AddChannelForSymbol<BinanceFuturesStreamPosition>(symbol);
            services.AddChannelForSymbol<PositionState>(symbol);
        }

        services.AddChannelForSymbol<BinanceFuturesStreamBalance>("BNB");
        services.AddChannelForSymbol<BinanceFuturesStreamBalance>("USDT");
        services.AddChannel<BinanceFuturesStreamPosition>();
        services.AddChannel<BinanceFuturesStreamBalance>();
        services.AddChannel<BinanceFuturesStreamOrderUpdate>();
        services.AddChannel<BinanceFuturesStreamTradeUpdate>();
        services.AddChannel<ExecutionResult>();
    }

    public static void AddCache<TCacheLoader, TData>(this IServiceCollection services)
        where TCacheLoader : CacheForSymbol<TData>
    {
        services.TryAddSingleton<ICacheForSymbol<TData>, TCacheLoader>();
        services.AddHostedService(provider => (TCacheLoader)provider.GetRequiredService<ICacheForSymbol<TData>>());
    }

    private static void AddChannelForSymbol<T>(this IServiceCollection services, string symbol)
    {
        var channel = CreateUnboundedChannel<T>();
        services.AddKeyedSingleton(symbol, channel.Reader);
        services.AddKeyedSingleton(symbol, channel.Writer);
    }

    private static void AddChannel<T>(this IServiceCollection services)
    {
        var channel = CreateUnboundedChannel<T>();
        services.TryAddSingleton(channel.Reader);
        services.TryAddSingleton(channel.Writer);
    }

    private static Channel<T> CreateUnboundedChannel<T>()
    {
        return Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = true,
            SingleReader = false,
            SingleWriter = true
        });
    }
}
