using Banana.Strategies.MeanReverse.Binance.Execution.Models;

namespace Banana.Strategies.MeanReverse.Binance.Execution.CombinedExecution;

public class CombinedExecutionCommand<TLaunchExecutionCommand> : LaunchExecutionCommandBase
    where TLaunchExecutionCommand : LaunchExecutionCommandBase
{
    public decimal PartSizePercent { get; set; }
    public CombinedExecutionType Type { get; set; }
    public required TLaunchExecutionCommand NestedExecutionsRules { get; set; }
    public int? ChunkSize { get; set; }
}

public enum CombinedExecutionType
{
    Synced,
    Parallel,
    Chunked
}
