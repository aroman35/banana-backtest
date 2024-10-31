namespace Banana.Backtest.Common.Models.MarketData;

using System.Runtime.InteropServices;
using Extensions;

[StructLayout(LayoutKind.Sequential)]
public struct MarketDataItem<TMarketData>(TMarketData item, long timestamp)
    where TMarketData : unmanaged
{
    public long Timestamp = timestamp;
    public TMarketData Item = item;

    public DateTime DateTime => Timestamp.AsDateTime();

    public DateOnly Date => DateOnly.FromDateTime(DateTime);

    public TimeOnly Time => TimeOnly.FromDateTime(DateTime);

    public override string ToString()
    {
        return $"[{Timestamp.AsDateTime().ToLocalTime():O}]: {Item.ToString()}";
    }
}
