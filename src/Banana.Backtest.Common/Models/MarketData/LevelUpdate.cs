namespace Banana.Backtest.Common.Models.MarketData;

using System.Runtime.InteropServices;

[Feed(FeedType.LevelUpdates)]
[StructLayout(LayoutKind.Sequential)]
public struct LevelUpdate
{
    public double Price;
    public double Quantity;
    public bool IsBid;
    public bool IsSnapshot;
}
