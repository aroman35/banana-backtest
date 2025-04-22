using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;
using Banana.Backtest.Emulator.Services;
using Microsoft.Extensions.Options;

namespace Banana.Backtest.Launcher.Launchers;

public class BacktestMatcherLauncher(
    IChannelsProvider channelsProvider,
    TimeProvider timeProvider,
    IOptions<MatcherSettings> settings,
    ILogger logger)
    : BacktestMatcher(channelsProvider, timeProvider, settings, logger), IHostedService
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
