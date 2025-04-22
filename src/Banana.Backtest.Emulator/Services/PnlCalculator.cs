using System.Collections.Concurrent;
using System.Threading.Channels;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;
using Serilog;

namespace Banana.Backtest.Emulator.Services;

public class PnlCalculator : IChannelSubscriber<UserExecution>
{
    private readonly ChannelReader<UserExecution> _executionsFeed;
    private readonly Task _executionsFeedTask;
    private readonly ConcurrentDictionary<long, UserExecution> _userExecutions = new();

    public PnlCalculator(IChannelsProvider channelsProvider, ILogger logger)
    {
        Logger = logger.ForContext<PnlCalculator>();
        _executionsFeed = channelsProvider.UserExecutionChannel.Reader;
        _executionsFeedTask = ((IChannelSubscriber<UserExecution>)this).SubscribeAsync();
    }

    public ValueTask HandleChannelDataAsync(UserExecution channelData, CancellationToken cancellationToken)
    {
        _userExecutions.TryAdd(channelData.OrderId, channelData);
        return ValueTask.CompletedTask;
    }

    ChannelReader<UserExecution> IChannelSubscriber<UserExecution>.Reader => _executionsFeed;

    public ILogger Logger { get; }

    public async ValueTask DisposeAsync()
    {
        await _executionsFeed.Completion;
        await _executionsFeedTask;
        Logger.Debug("Disposed. Total trades: {TotalTrades}", _userExecutions.Count);
    }
}
