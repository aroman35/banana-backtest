using Banana.Strategies.MeanReverse.Binance.Execution.Models;

namespace Banana.Strategies.MeanReverse.Binance.Execution.FrontRun;

public class FrontRunExecutionLaunchCommand : LaunchExecutionCommandBase
{
    public bool IsMakerOnly { get; set; }
    public decimal IcebergDivider { get; set; } = 1;
}
