using System.Runtime.InteropServices;

namespace Banana.Backtest.Common.Models.MarketData;

[Feed(FeedType.LevelUpdates)]
[StructLayout(LayoutKind.Sequential)]
public struct LevelUpdate
{
    public double Price;
    public double Quantity;
    public bool IsBid;
    public bool IsSnapshot;
}