using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;
using Banana.Backtest.Emulator.Services;

namespace Banana.Backtest.Launcher;

public class DummyStrategy(IChannelsProvider channelsProvider, TimeProvider timeProvider, ILogger logger)
    : StrategyBase(channelsProvider, timeProvider, logger)
{
    public override ValueTask HandleChannelDataAsync(UserExecution channelData, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    public override ValueTask HandleChannelDataAsync(OrderInfo channelData, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    public override ValueTask HandleChannelDataAsync(MarketDataItem channelData, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
