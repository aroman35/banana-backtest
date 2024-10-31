using System.Text;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.Common.Services;

namespace Banana.Backtest.CryptoConverter.Parsers;

public class TardisParser<TMarketDataType>(
    Stream tardisDecompressionStream,
    ILineParser<TMarketDataType> lineParser,
    IParserHandler<TMarketDataType> handler,
    Symbol symbol,
    ILogger logger)
    : AbstractCsvParser<TMarketDataType>(tardisDecompressionStream, handler, "\n"u8.ToArray())
    where TMarketDataType : unmanaged
{
    private readonly ILogger _logger = logger.ForContext<TardisParser<TMarketDataType>>();
    private int _lineNumber;

    protected override bool ParseLine(Span<byte> line, out MarketDataItem<TMarketDataType> marketDataItem, out Symbol symbol1)
    {
        marketDataItem = default;
        symbol1 = symbol;

        try
        {
            if (_lineNumber == 0)
                return false;

            return lineParser.ParseLine(line, out marketDataItem);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Unable to parse line: {Line}", Encoding.UTF8.GetString(line));
            return false;
        }
        finally
        {
            _lineNumber++;
        }
    }

    protected override void Dispose(bool disposing)
    {
    }
}
