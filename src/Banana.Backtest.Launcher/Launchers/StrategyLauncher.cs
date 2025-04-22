using Banana.Backtest.Emulator.Services;

namespace Banana.Backtest.Launcher.Launchers;

public class StrategyLauncher(StrategyBase strategy) : IHostedService, IAsyncDisposable
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return strategy.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return strategy.StopAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await strategy.DisposeAsync();
    }
}
