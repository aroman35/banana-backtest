namespace Banana.Strategies.MeanReverse.Binance.UserData.RiskManagement;

public class CreateRiskManagementCommand
{
    public required string Symbol { get; init; }
    public decimal StopLossPercent { get; init; }
    public decimal IncreasePositionPercent { get; init; }
    public decimal InitialPrice { get; init; }
    public decimal InitialQuantity { get; init; }
    public Guid? ConnectedExecutionId { get; init; }
}
