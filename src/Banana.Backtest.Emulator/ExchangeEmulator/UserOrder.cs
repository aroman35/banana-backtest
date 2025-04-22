using System.Runtime.InteropServices;
using Banana.Backtest.Common.Models;

namespace Banana.Backtest.Emulator.ExchangeEmulator;

[Obsolete]
[StructLayout(LayoutKind.Sequential)]
public struct UserOrder
{
    public long Id;
    public OrderType OrderType;
    public Side Side;
    public double Price;
    public double Quantity;
    public long Timestamp;
    public Guid ClientOrderId;

    public UserOrder PartiallyFill(double executedQuantity)
    {
        return this with
        {
            Quantity = Quantity - executedQuantity
        };
    }
}

public enum OrderType
{
    Market,
    Limit
}
