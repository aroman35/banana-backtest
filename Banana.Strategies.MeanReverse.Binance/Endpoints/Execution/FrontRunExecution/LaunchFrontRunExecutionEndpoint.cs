using Banana.Strategies.MeanReverse.Binance.Execution;
using Banana.Strategies.MeanReverse.Binance.Execution.FrontRun;
using Banana.Strategies.MeanReverse.Binance.Execution.Models;
using Banana.Strategies.MeanReverse.Binance.UserData.RiskManagement;
using FastEndpoints;

namespace Banana.Strategies.MeanReverse.Binance.Endpoints.Execution.FrontRunExecution;

public class LaunchFrontRunExecutionEndpoint(
    DeferredExecution execution,
    DeferredRiskManagement riskManagement,
    IHostApplicationLifetime hostApplicationLifetime) : Endpoint<LaunchFrontRunExecutionRequest, ExecutionResult>
{
    public override void Configure()
    {
        Post("/execution/front-run-execution");
        AllowAnonymous();
    }

    public override async Task<ExecutionResult> ExecuteAsync(LaunchFrontRunExecutionRequest request, CancellationToken cancellationToken)
    {
        var result = await execution.Execute<FrontRunExecutionLaunchCommand>(request, cancellationToken);
        if (result.Status is ExecutionResult.ExecutionStatus.Finished)
        {
            var riskManagementSettings = new CreateRiskManagementCommand
            {
                Symbol = request.Symbol,
                StopLossPercent = request.StopLossPercent,
                InitialPrice = result.MeanExecutionPrice,
                ConnectedExecutionId = result.Id
            };
            _ = Task.Run(() => riskManagement.Execute(riskManagementSettings, hostApplicationLifetime.ApplicationStopped));
        }
        return result;
    }
}
