using Banana.Backtest.Common.Models;

namespace Banana.Strategies.MeanReverse.Binance.Execution.Models;

public class LaunchExecutionCommandBase : ICloneable
{
    public required string Symbol { get; set; }
    public Side Side { get; set; }
    public decimal Quantity { get; set; }
    public TimeSpan Timeout { get; set; }
    public QuantitySpread QuantitySpread { get; set; }

    public object Clone()
    {
        return MemberwiseClone();
    }

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (Quantity <= 0)
        {
            error = "Requested negative quantity";
            return false;
        }

        if (Side is Side.Undefined)
        {
            error = "Side is missing";
            return false;
        }

        if (Timeout <= TimeSpan.Zero)
        {
            error = "Timeout was not set";
            return false;
        }
        return true;
    }
}

public enum QuantitySpread
{
    NoSpread,
    Upper,
    Lower
}
