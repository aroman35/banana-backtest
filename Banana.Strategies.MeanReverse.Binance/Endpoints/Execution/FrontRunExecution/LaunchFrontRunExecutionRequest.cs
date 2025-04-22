using Banana.Strategies.MeanReverse.Binance.Execution.FrontRun;

namespace Banana.Strategies.MeanReverse.Binance.Endpoints.Execution.FrontRunExecution;

public class LaunchFrontRunExecutionRequest : FrontRunExecutionLaunchCommand
{
    public decimal StopLossPercent { get; set; }
}
