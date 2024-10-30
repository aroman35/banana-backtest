using Banana.Backtest.MoexConverter.Converters;
using CommandLine;
using Serilog;

var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .MinimumLevel.Information()
    .CreateLogger();

var settingsParseResult = Parser.Default.ParseArguments<SourcesConverterSettings>(args);
settingsParseResult.WithParsed(settings =>
{
    ArgumentException.ThrowIfNullOrWhiteSpace(settings.OutputDirectoryPath);
    var converter = new SourceDataConverter(settings, logger);
    if (settings.IsTradesParsingRequested)
        converter.ConvertTrades();
    if (settings.IsOrdersParsingRequested)
        converter.ConvertOrders();
});

Log.CloseAndFlush();