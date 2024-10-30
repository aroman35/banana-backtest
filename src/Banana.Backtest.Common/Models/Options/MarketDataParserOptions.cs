using System.IO.Compression;

namespace Banana.Backtest.Common.Models.Options;

public class MarketDataParserOptions
{
    public string? OutputDirectory { get; set; }
    public CompressionLevel CompressionLevel { get; set; }
    public CompressionType CompressionType { get; set; }
}