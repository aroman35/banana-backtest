using System.Threading.Channels;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.Abstractions;

namespace Banana.Strategies.MeanReverse.Launchers;

public class LiveMarketDataChannelsProvider<TMarketData> : IMarketDataChannelsProvider<TMarketData>
    where TMarketData : unmanaged
{
    public Channel<MarketDataItem<TMarketData>> MarketDataSourceChannel { get; } =
        Channel.CreateUnbounded<MarketDataItem<TMarketData>>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = true,
            SingleReader = false,
            SingleWriter = true
        });
    public Channel<MarketDataItem<TMarketData>> MarketDataGatewayChannel { get; } =
        Channel.CreateUnbounded<MarketDataItem<TMarketData>>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = true,
            SingleReader = false,
            SingleWriter = true
        });
}
