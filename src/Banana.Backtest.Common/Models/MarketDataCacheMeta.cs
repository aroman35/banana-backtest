using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Banana.Backtest.Common.Models;

[StructLayout(LayoutKind.Sequential)]
public struct MarketDataCacheMeta
{
    public MarketDataHash Hash;
    public CompressionType CompressionType;
    public CompressionLevel CompressionLevel;
    public long ItemsCount;
    public DateTime BuildTime;
    public Version Version;
}