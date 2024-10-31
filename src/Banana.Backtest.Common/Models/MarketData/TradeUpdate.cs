using System.Runtime.InteropServices;

namespace Banana.Backtest.Common.Models.MarketData;

[Feed(FeedType.Trades)]
[StructLayout(LayoutKind.Sequential)]
public struct TradeUpdate
{
    public Side Side;
    public double Price;
    public double Quantity;
    public long TradeId;

    public bool IsBuyer => Side is Side.Long;
    public double Volume => Price * Quantity;

    public override string ToString()
    {
        return $"{Side} {Price}x{Quantity}";
    }
}
