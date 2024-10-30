using System.Runtime.InteropServices;

namespace Banana.Backtest.Common.Models.MarketData;

[Feed(FeedType.OrdersLog)]
[StructLayout(LayoutKind.Sequential)]
public struct OrderUpdate
{
    public long OrderId;
    public Side Side;
    public long Timestamp;
    public double Price;
    public double Quantity;
    public EntryType Type;
    public long TradeId;
    public double ExecutionPrice;

    public override string ToString()
    {
        return $"[{DateTimeOffset.FromUnixTimeMilliseconds(Timestamp):O}] {Type}: {Side} {Price}x{Quantity}";
    }
}