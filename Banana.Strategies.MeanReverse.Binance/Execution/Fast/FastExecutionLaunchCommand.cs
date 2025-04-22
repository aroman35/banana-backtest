using Banana.Strategies.MeanReverse.Binance.Execution.Models;

namespace Banana.Strategies.MeanReverse.Binance.Execution.Fast;

public class FastExecutionLaunchCommand : LaunchExecutionCommandBase
{
    public FastExecutionType Type { get; set; }
}

public enum FastExecutionType
{
    BestOffer,
    Market
}
