using Banana.Backtest.Emulator.Services;
using Banana.Backtest.Launcher.Steps.Common;

namespace Banana.Backtest.Launcher.Steps;

public class StrategyStep(StrategyBase strategy) : IBacktestStep, IAsyncDisposable
{
    public int Priority => 5;
    public bool IsBackground => true;
    public Task WaitForCompletion(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await strategy.DisposeAsync();
    }
}
