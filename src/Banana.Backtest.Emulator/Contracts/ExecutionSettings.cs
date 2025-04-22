using Banana.Backtest.Common.Models;

namespace Banana.Backtest.Emulator.Contracts;

public class ExecutionSettings
{
    public Side Side { get; set; }
    public bool IsMakerOnly { get; set; }
    public double RequestedQuantity { get; set; }
    public double PriceLimit { get; set; }
    public double PriceSpread { get; set; }
}
