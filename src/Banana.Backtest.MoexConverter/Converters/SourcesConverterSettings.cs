using System.IO.Compression;
using Banana.Backtest.Common.Models;
using CommandLine;

namespace Banana.Backtest.MoexConverter.Converters;

public class SourcesConverterSettings
{
    private const string COMPRESSION_LEVEL_HELP = "Compression level (Optimal, Fastest, NoCompression, SmallestSize), dafault = Optimal";
    private const string COMPRESSION_TYPES_HELP = "Compression type (NoCompression, GZip, Brotli, Deflate), default = Brotli";

    [Option('o', "ordersPath", Required = false, HelpText = "Path to orders source file")]
    public string? OrdersLogFilePath { get; set; }
    [Option('t', "tradesPath", Required = false, HelpText = "Path to trades source file")]
    public string? TradesFilePath { get; set; }
    [Option('d', "destinationDirectory", Required = true, HelpText = "Path to directory destination")]
    public string? OutputDirectoryPath { get; set; }
    [Option('l', "compressionLevel", Required = false, HelpText = COMPRESSION_LEVEL_HELP, Default = CompressionLevel.Fastest)]
    public CompressionLevel CompressionLevel { get; set; }
    [Option('c', "compressionType", Required = false, HelpText = COMPRESSION_TYPES_HELP, Default = CompressionType.GZip)]
    public CompressionType CompressionType { get; set; }

    public bool IsOrdersParsingRequested => !string.IsNullOrWhiteSpace(OrdersLogFilePath);
    public bool IsTradesParsingRequested => !string.IsNullOrWhiteSpace(TradesFilePath);
}