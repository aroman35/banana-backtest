using System.Diagnostics;
using System.Text.RegularExpressions;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Options;
using Banana.Backtest.Common.Services;
using Banana.Backtest.MoexConverter.Parsers;
using Microsoft.Extensions.Options;
using Serilog;
using SevenZipExtractor;

namespace Banana.Backtest.MoexConverter.Converters;

public partial class SourceDataConverter(SourcesConverterSettings settings, ILogger logger)
{
    public void ConvertOrders(Stream? dataStream = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.OutputDirectoryPath);
        if (dataStream is null)
            ArgumentException.ThrowIfNullOrWhiteSpace(settings.OrdersLogFilePath);
        var startedTimestamp = Stopwatch.GetTimestamp();
        var marketDataParserHandlerOptions = Options.Create(new MarketDataParserOptions
        {
            OutputDirectory = settings.OutputDirectoryPath,
            CompressionType = settings.CompressionType,
            CompressionLevel = settings.CompressionLevel,
        });
        using var ordersHandler = new MarketDataParserHandler<OrderUpdate>(marketDataParserHandlerOptions, logger);
        using var ordersParser = dataStream is null
            ? new OrdersLogParser(settings.OrdersLogFilePath!, ordersHandler, logger)
            : new OrdersLogParser(dataStream, ordersHandler, logger);
        ordersParser.ProcessCsvFile();
        var elapsed = Stopwatch.GetElapsedTime(startedTimestamp);
        logger.Information("Orders converting finished in {Elapsed} ms", elapsed);
    }

    public void ConvertTrades(Stream? dataStream = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.OutputDirectoryPath);
        if (dataStream is null)
            ArgumentException.ThrowIfNullOrWhiteSpace(settings.TradesFilePath);

        var startedTimestamp = Stopwatch.GetTimestamp();
        var marketDataParserHandlerOptions = Options.Create(new MarketDataParserOptions
        {
            OutputDirectory = settings.OutputDirectoryPath,
            CompressionType = settings.CompressionType,
            CompressionLevel = settings.CompressionLevel,
        });
        using var executionsHandler = new MarketDataParserHandler<TradeUpdate>(marketDataParserHandlerOptions, logger);
        using var executionsParser = dataStream is null
            ? new TradesLogParser(settings.TradesFilePath!, executionsHandler, logger)
            : new TradesLogParser(dataStream, executionsHandler, logger);
        executionsParser.ProcessCsvFile();
        var elapsed = Stopwatch.GetElapsedTime(startedTimestamp);
        logger.Information("Trades converting finished in {Elapsed} ms", elapsed);
    }

    public void FindAndConvertFromDirectory()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.OutputDirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.InputDirectory);

        foreach (var fullFileName in Directory.EnumerateFiles(settings.InputDirectory, "*.7z", SearchOption.TopDirectoryOnly))
        {
            var tradesArchive = TradesFileNameRegex().Match(fullFileName);
            var ordersArchive = OrdersFileNameRegex().Match(fullFileName);

            if (tradesArchive.Success || ordersArchive.Success)
            {
                var shortFileName = tradesArchive.Success ? tradesArchive.Value : ordersArchive.Value;
                using var sourceFile = new FileStream(fullFileName, new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    BufferSize = 1024 * 1024,
                    Mode = FileMode.Open,
                    Options = FileOptions.Asynchronous | FileOptions.RandomAccess,
                    Share = FileShare.Read
                });
                using var archiveFile = new ArchiveFile(sourceFile, SevenZipFormat.SevenZip);
                if (GetDataFile(archiveFile, shortFileName, out var dataFileStream))
                {
                    logger.Information("Processing source file {SourceFileName}", fullFileName);
                    using (dataFileStream)
                    {
                        if (tradesArchive.Success)
                            ConvertTrades(dataFileStream);
                        if (ordersArchive.Success)
                            ConvertOrders(dataFileStream);
                    }
                    logger.Information("Finished processing source file {SourceFileName}", fullFileName);
                }
            }
        }
    }

    private bool GetDataFile(ArchiveFile archiveFile, string sourceFileName, out Stream dataFileStream)
    {
        var expectedFileName = sourceFileName.Replace(".7z", ".csv");
        var dataFile = archiveFile.Entries.FirstOrDefault(x => x.FileName == expectedFileName);
        if (dataFile is null)
        {
            logger.Warning("Data file ({ExpectedName}) was not found in source archive: {SourceFileName}", expectedFileName, sourceFileName);
            dataFileStream = Stream.Null;
            return false;
        }

        dataFileStream = new MemoryStream();
        dataFile.Extract(dataFileStream);
        return true;
    }

    [GeneratedRegex(@"\d{6}_fut_deal.7z$")]
    private static partial Regex TradesFileNameRegex();

    [GeneratedRegex(@"\d{6}_fut_log.7z$")]
    private static partial Regex OrdersFileNameRegex();
}
