using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Services;

namespace Banana.Backtest.Launcher.Launchers;

public class BacktestClockLauncher(IChannelsProvider channelsProvider, ILogger logger)
    : BacktestClock(channelsProvider, logger), IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
