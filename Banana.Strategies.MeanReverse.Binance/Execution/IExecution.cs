using Banana.Strategies.MeanReverse.Binance.Execution.Models;

namespace Banana.Strategies.MeanReverse.Binance.Execution;

public interface IExecution<TLaunchExecutionCommand>
    where TLaunchExecutionCommand : LaunchExecutionCommandBase
{
    Task<ExecutionResult> ExecuteAsync(TLaunchExecutionCommand launchCommand, CancellationToken cancellationToken);
}
