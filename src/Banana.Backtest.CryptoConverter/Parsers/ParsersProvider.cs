using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.Options;
using Microsoft.Extensions.Options;

namespace Banana.Backtest.CryptoConverter.Parsers;

public class ParsersProvider(
    ILogger logger,
    IOptions<MarketDataParserOptions> options,
    IServiceScopeFactory scopeFactory)
{
    private readonly ILogger _logger = logger.ForContext<ParsersProvider>();

    public void ParseTardisSource<TMarketDataType>(
        Stream tardisDecompressionStream,
        MarketDataHash hash)
        where TMarketDataType : unmanaged
    {
        using var scope = scopeFactory.CreateScope();
        var lineParser = scope.ServiceProvider.GetRequiredService<ILineParser<TMarketDataType>>();
        using var handler = new TardisParserHandler<TMarketDataType>(options, hash);
        using var parser = new TardisParser<TMarketDataType>(tardisDecompressionStream, lineParser, handler, hash.Symbol, _logger);
        parser.ProcessCsvFile();
    }
}