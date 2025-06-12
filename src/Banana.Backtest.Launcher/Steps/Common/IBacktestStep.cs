namespace Banana.Backtest.Launcher.Steps.Common;

public interface IBacktestStep
{
    int Priority { get; }
    bool IsBackground { get; }
    Task IsInitialized => Task.CompletedTask;
    Task WaitForCompletion(CancellationToken cancellationToken = default);
}
