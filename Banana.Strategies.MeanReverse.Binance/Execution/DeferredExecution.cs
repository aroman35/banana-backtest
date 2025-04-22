using Banana.Strategies.MeanReverse.Binance.Execution.Models;

namespace Banana.Strategies.MeanReverse.Binance.Execution;

public class DeferredExecution(IServiceScopeFactory scopeFactory)
{
    public async Task<ExecutionResult> Execute<TLaunchExecutionCommandBase>(TLaunchExecutionCommandBase command, CancellationToken cancellationToken)
        where TLaunchExecutionCommandBase : LaunchExecutionCommandBase
    {
        await using (var serviceScope = scopeFactory.CreateAsyncScope())
        {
            var execution = serviceScope.ServiceProvider.GetRequiredService<IExecution<TLaunchExecutionCommandBase>>();
            var result = await execution.ExecuteAsync(command, cancellationToken);
            return result;
        }
    }
}
