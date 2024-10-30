using System.Globalization;
using System.Text;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.Common.Services;
using Serilog;

namespace Banana.Backtest.MoexConverter.Parsers;

public class TradesLogParser(string filePath, IParserHandler<TradeUpdate> parserHandler, ILogger logger)
    : AbstractCsvParser<TradeUpdate>(File.Open(filePath, FileMode.Open, FileAccess.Read), parserHandler)
{
    private readonly ILogger _logger = logger.ForContext<TradesLogParser>();

    protected override bool ParseLine(Span<byte> line, out MarketDataItem<TradeUpdate> marketDataItem, out Symbol symbol)
    {
        // #SYMBOL,SYSTEM,MOMENT,ID_DEAL,PRICE_DEAL,VOLUME,OPEN_POS,DIRECTION
        marketDataItem = default;
        symbol = default;
        
        if (line.Length == 0)
            return false;
        if (line[0] == '#')
            return false;

        try
        {
            // SYMBOL
            var indexOfComma = line.IndexOf(COMMA);
            var symbolSpan = line[..indexOfComma];
            symbol = Symbol.Create(Asset.Parse(symbolSpan), Asset.SPBFUT, Exchange.MoexFutures);
            var remainedLine = line[(indexOfComma + 1)..];
            
            // SYSTEM
            indexOfComma = remainedLine.IndexOf(COMMA);
            remainedLine = remainedLine[(indexOfComma + 1)..];
            
            // MOMENT
            indexOfComma = remainedLine.IndexOf(COMMA);
            var timestampSpanBytes = remainedLine[..indexOfComma];
            Span<char> timestampSpanChars = stackalloc char[timestampSpanBytes.Length];
            var charsLength = Encoding.UTF8.GetChars(timestampSpanBytes, timestampSpanChars);
            var timestamp = DateTimeOffset.ParseExact(
                timestampSpanChars[..charsLength],
                TIMESTAMP_FORMAT,
                CultureInfo.InvariantCulture)
                .ToUnixTimeMilliseconds();
            
            remainedLine = remainedLine[(indexOfComma + 1)..];
            
            // ID_DEAL
            indexOfComma = remainedLine.IndexOf(COMMA);
            var tradeIdSpan = remainedLine[..indexOfComma];
            var tradeId = long.Parse(tradeIdSpan, CultureInfo.InvariantCulture);
            remainedLine = remainedLine[(indexOfComma + 1)..];
            
            // PRICE_DEAL
            indexOfComma = remainedLine.IndexOf(COMMA);
            var priceSpan = remainedLine[..indexOfComma];
            var price = double.Parse(priceSpan, CultureInfo.InvariantCulture);
            remainedLine = remainedLine[(indexOfComma + 1)..];

            // VOLUME
            indexOfComma = remainedLine.IndexOf(COMMA);
            var quantitySpan = remainedLine[..indexOfComma];
            var quantity = long.Parse(quantitySpan, CultureInfo.InvariantCulture);
            remainedLine = remainedLine[(indexOfComma + 1)..];
            
            // OPEN_POS
            indexOfComma = remainedLine.IndexOf(COMMA);
            remainedLine = remainedLine[(indexOfComma + 1)..];
            
            // DIRECTION
            var side = remainedLine[0] == BUY_SIDE ? Side.Long : Side.Short;

            
            marketDataItem = new MarketDataItem<TradeUpdate>
            {
                Timestamp = timestamp,
                Item = new TradeUpdate
                {
                    Side = side,
                    Price = price,
                    Quantity = quantity,
                    TradeId = tradeId
                }
            };

            return true;
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Unable to parse line: {Line}", Encoding.UTF8.GetString(line));
            return false;
        }
    }
}