using Banana.Backtest.Emulator.Services;
using Banana.Backtest.Launcher.Steps.Common;

namespace Banana.Backtest.Launcher.Steps;

public class EmulatorStep(BacktestMatcher matcher) : IBacktestStep
{
    public int Priority => 4;
    public bool IsBackground => true;
    public Task WaitForCompletion(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
