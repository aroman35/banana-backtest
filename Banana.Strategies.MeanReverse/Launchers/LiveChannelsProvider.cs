using System.Threading.Channels;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;

namespace Banana.Strategies.MeanReverse.Launchers;

public class LiveChannelsProvider(IServiceProvider serviceProvider) : IChannelsProvider
{
    public Channel<MarketDataItem<TMarketData>> GetMarketDataSourceChannel<TMarketData>() where TMarketData : unmanaged
    {
        return serviceProvider.GetRequiredService<IMarketDataChannelsProvider<TMarketData>>().MarketDataSourceChannel;
    }

    public Channel<MarketDataItem<TMarketData>> GetMarketDataGatewayChannel<TMarketData>() where TMarketData : unmanaged
    {
        return serviceProvider.GetRequiredService<IMarketDataChannelsProvider<TMarketData>>().MarketDataGatewayChannel;
    }

    public Channel<UserExecution> UserExecutionChannel { get; } =
        Channel.CreateUnbounded<UserExecution>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = true,
            SingleReader = false,
            SingleWriter = true
        });

    public Channel<PlaceOrderRequest> UserOrdersChannel { get; } =
        Channel.CreateUnbounded<PlaceOrderRequest>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = true,
            SingleReader = false,
            SingleWriter = true
        });

    public Channel<CancelOrderRequest> CancelOrdersChannel { get; } =
        Channel.CreateUnbounded<CancelOrderRequest>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = true,
            SingleReader = false,
            SingleWriter = true
        });

    public Channel<OrderInfo> OrderStatusesChannel { get; } =
        Channel.CreateUnbounded<OrderInfo>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = true,
            SingleReader = false,
            SingleWriter = true
        });

    public Channel<long> TimestampsFeed { get; } =
        Channel.CreateUnbounded<long>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = true,
            SingleReader = false,
            SingleWriter = true
        });

    public Channel<MarketDataItem> MarketDataCommonProviderChannel { get; } =
        Channel.CreateUnbounded<MarketDataItem>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = true,
            SingleReader = false,
            SingleWriter = true
        });
}
