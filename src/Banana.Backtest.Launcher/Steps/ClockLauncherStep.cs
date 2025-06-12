using Banana.Backtest.Launcher.Steps.Common;

namespace Banana.Backtest.Launcher.Steps;

public class ClockLauncherStep(TimeProvider timeProvider) : IBacktestStep
{
    public int Priority => 2;
    public bool IsBackground => false;

    public Task WaitForCompletion(CancellationToken cancellationToken = default)
    {
        _ = timeProvider.GetUtcNow();
        return Task.CompletedTask;
    }
}
