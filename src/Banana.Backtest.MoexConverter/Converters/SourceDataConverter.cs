using System.Diagnostics;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Options;
using Banana.Backtest.Common.Services;
using Banana.Backtest.MoexConverter.Parsers;
using Microsoft.Extensions.Options;
using Serilog;

namespace Banana.Backtest.MoexConverter.Converters;

public class SourceDataConverter(SourcesConverterSettings settings, ILogger logger)
{
    public void ConvertOrders()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.OutputDirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.OrdersLogFilePath);
        var startedTimestamp = Stopwatch.GetTimestamp();
        var marketDataParserHandlerOptions = Options.Create(new MarketDataParserOptions
        {
            OutputDirectory = settings.OutputDirectoryPath,
            CompressionType = settings.CompressionType,
            CompressionLevel = settings.CompressionLevel,
        });
        var ordersHandler = new MarketDataParserHandler<OrderUpdate>(marketDataParserHandlerOptions, logger);
        using var ordersParser = new OrdersLogParser(settings.OrdersLogFilePath, ordersHandler, logger);
        ordersParser.ProcessCsvFile();
        var elapsed = Stopwatch.GetElapsedTime(startedTimestamp);
        logger.Information("Orders converting finished in {Elapsed} ms", elapsed);
    }

    public void ConvertTrades()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.OutputDirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.TradesFilePath);
        var startedTimestamp = Stopwatch.GetTimestamp();
        var marketDataParserHandlerOptions = Options.Create(new MarketDataParserOptions
        {
            OutputDirectory = settings.OutputDirectoryPath,
            CompressionType = settings.CompressionType,
            CompressionLevel = settings.CompressionLevel,
        });
        var executionsHandler = new MarketDataParserHandler<TradeUpdate>(marketDataParserHandlerOptions, logger);
        using var executionsParser = new TradesLogParser(settings.TradesFilePath, executionsHandler, logger);
        executionsParser.ProcessCsvFile();
        var elapsed = Stopwatch.GetElapsedTime(startedTimestamp);
        logger.Information("Trades converting finished in {Elapsed} ms", elapsed);
    }
}