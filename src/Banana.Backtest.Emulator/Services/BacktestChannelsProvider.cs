using System.Threading.Channels;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Banana.Backtest.Emulator.Services;

/// <inheritdoc />
public class BacktestChannelsProvider(IServiceProvider serviceProvider) : IChannelsProvider
{
    /// <inheritdoc />
    public Channel<MarketDataItem<TMarketData>> GetMarketDataSourceChannel<TMarketData>()
        where TMarketData : unmanaged
    {
        return serviceProvider.GetRequiredService<IMarketDataChannelsProvider<TMarketData>>().MarketDataSourceChannel;
    }

    /// <inheritdoc />
    public Channel<MarketDataItem<TMarketData>> GetMarketDataGatewayChannel<TMarketData>()
        where TMarketData : unmanaged
    {
        return serviceProvider.GetRequiredService<IMarketDataChannelsProvider<TMarketData>>().MarketDataGatewayChannel;
    }

    /// <inheritdoc />
    public Channel<UserExecution> UserExecutionChannel { get; } =
        Channel.CreateBounded<UserExecution>(
            new BoundedChannelOptions(1)
            {
                SingleWriter = true,
                SingleReader = false,
                AllowSynchronousContinuations = true,
                FullMode = BoundedChannelFullMode.Wait
            });

    /// <inheritdoc />
    public Channel<PlaceOrderRequest> UserOrdersChannel { get; } =
        Channel.CreateBounded<PlaceOrderRequest>(new BoundedChannelOptions(1)
        {
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = true,
            FullMode = BoundedChannelFullMode.Wait
        });

    public Channel<CancelOrderRequest> CancelOrdersChannel { get; } =
        Channel.CreateBounded<CancelOrderRequest>(new BoundedChannelOptions(1)
        {
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = true,
            FullMode = BoundedChannelFullMode.Wait
        });

    /// <inheritdoc />
    public Channel<OrderInfo> OrderStatusesChannel { get; } =
        Channel.CreateBounded<OrderInfo>(new BoundedChannelOptions(1)
        {
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = true,
            FullMode = BoundedChannelFullMode.Wait
        });

    /// <inheritdoc />
    public Channel<long> TimestampsFeed { get; } =
        Channel.CreateBounded<long>(new BoundedChannelOptions(1)
        {
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = true,
            FullMode = BoundedChannelFullMode.Wait
        });

    public Channel<MarketDataItem> MarketDataCommonProviderChannel { get; } =
        Channel.CreateBounded<MarketDataItem>(new BoundedChannelOptions(1)
        {
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = true,
            FullMode = BoundedChannelFullMode.Wait
        });
}
