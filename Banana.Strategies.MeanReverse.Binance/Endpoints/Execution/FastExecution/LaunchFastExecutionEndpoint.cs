using Banana.Strategies.MeanReverse.Binance.Execution;
using Banana.Strategies.MeanReverse.Binance.Execution.Fast;
using Banana.Strategies.MeanReverse.Binance.Execution.Models;
using FastEndpoints;

namespace Banana.Strategies.MeanReverse.Binance.Endpoints.Execution.FastExecution;

public class LaunchFastExecutionEndpoint(DeferredExecution execution) : Endpoint<LaunchFastExecutionRequest, ExecutionResult>
{
    public override void Configure()
    {
        Post("/execution/fast-execution");
        AllowAnonymous();
    }

    public override async Task<ExecutionResult> ExecuteAsync(LaunchFastExecutionRequest request, CancellationToken cancellationToken)
    {
        var result = await execution.Execute<FastExecutionLaunchCommand>(request, cancellationToken);
        return result;
    }
}
