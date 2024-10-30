using System.IO.Compression;
using Banana.Backtest.Common.Models;

namespace Banana.Backtest.Common.Services;

public class LevelUpdatesConvertorSettings
{
    public required string? StoragePath { get; set; }
    public required string? OutputDirectoryPath { get; set; }
    public required MarketDataHash Hash { get; set; }
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.NoCompression;
    public CompressionType CompressionType { get; set; } = CompressionType.NoCompression;
}