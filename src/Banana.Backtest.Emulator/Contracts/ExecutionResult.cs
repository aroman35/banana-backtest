namespace Banana.Backtest.Emulator.Contracts;

public struct ExecutionResult
{
    public int TradesCount { get; set; }
    public int PlacedOrdersCount { get; set; }
    public int CancelledOrdersCount { get; set; }
    public double ExecutedQuantity { get; set; }
    public double MeanExecutionPrice { get; set; }
}
