using System.Threading.Channels;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.Abstractions;

namespace Banana.Backtest.Emulator.Services;

/// <inheritdoc />
public class BacktestMarketDataChannelsProvider<TMarketData> : IMarketDataChannelsProvider<TMarketData>
    where TMarketData : unmanaged
{
    /// <inheritdoc />
    public Channel<MarketDataItem<TMarketData>> MarketDataSourceChannel { get; } =
        Channel.CreateBounded<MarketDataItem<TMarketData>>(new BoundedChannelOptions(1)
        {
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = true,
            FullMode = BoundedChannelFullMode.Wait
        });

    /// <inheritdoc />
    public Channel<MarketDataItem<TMarketData>> MarketDataGatewayChannel { get; } =
        Channel.CreateBounded<MarketDataItem<TMarketData>>(new BoundedChannelOptions(1)
        {
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = true,
            FullMode = BoundedChannelFullMode.Wait
        });
}
