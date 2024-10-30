using System.Globalization;
using System.Text;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.Common.Services;
using Serilog;

namespace Banana.Backtest.MoexConverter.Parsers;

public class OrdersLogParser(string filePath, IParserHandler<OrderUpdate> parserHandler, ILogger logger)
    : AbstractCsvParser<OrderUpdate>(filePath, parserHandler)
{
    private readonly ILogger _logger = logger.ForContext<OrdersLogParser>();

    protected override unsafe bool ParseLine(Span<byte> line, out MarketDataItem<OrderUpdate> marketDataItem, out Symbol symbol)
    {
        // #SYMBOL,SYSTEM,TYPE,MOMENT,ID,ACTION,PRICE,VOLUME,ID_DEAL,PRICE_DEAL
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

            // TYPE
            indexOfComma = remainedLine.IndexOf(COMMA);
            var sideSpan = remainedLine[..indexOfComma];
            var side = sideSpan[0] == BUY_SIDE ? Side.Long : Side.Short;
            remainedLine = remainedLine[(indexOfComma + 1)..];

            // MOMENT
            indexOfComma = remainedLine.IndexOf(COMMA);
            var timestampSpanBytes = remainedLine[..indexOfComma];
            Span<char> timestampSpanChars = stackalloc char[timestampSpanBytes.Length];
            var charsLength = Encoding.UTF8.GetChars(timestampSpanBytes, timestampSpanChars);
            var timestamp = DateTimeOffset.ParseExact(
                timestampSpanChars[..charsLength],
                TIMESTAMP_FORMAT,
                CultureInfo.InvariantCulture).ToUnixTimeMilliseconds();

            remainedLine = remainedLine[(indexOfComma + 1)..];

            // ID
            indexOfComma = remainedLine.IndexOf(COMMA);
            var idSpan = remainedLine[..indexOfComma];
            var orderId = long.Parse(idSpan, CultureInfo.InvariantCulture);
            remainedLine = remainedLine[(indexOfComma + 1)..];

            // ACTION
            indexOfComma = remainedLine.IndexOf(COMMA);
            var typeSpan = remainedLine[..indexOfComma];
            var type = (EntryType)short.Parse(typeSpan, CultureInfo.InvariantCulture);
            remainedLine = remainedLine[(indexOfComma + 1)..];

            // PRICE
            indexOfComma = remainedLine.IndexOf(COMMA);
            var priceSpan = remainedLine[..indexOfComma];
            var price = double.Parse(priceSpan, CultureInfo.InvariantCulture);
            remainedLine = remainedLine[(indexOfComma + 1)..];

            // VOLUME
            indexOfComma = remainedLine.IndexOf(COMMA);
            var quantitySpan = remainedLine[..indexOfComma];
            var quantity = long.Parse(quantitySpan, CultureInfo.InvariantCulture);
            remainedLine = remainedLine[(indexOfComma + 1)..];
            
            // ID_DEAL
            indexOfComma = remainedLine.IndexOf(COMMA);
            var tradeIdSpan = remainedLine[..indexOfComma];
            var tradeId = tradeIdSpan.Length > 0 ? long.Parse(tradeIdSpan, CultureInfo.InvariantCulture) : -1;
            remainedLine = remainedLine[(indexOfComma + 1)..];
            
            // PRICE_DEAL
            var executionPrice = remainedLine.Length > 0 ? double.Parse(remainedLine, CultureInfo.InvariantCulture) : double.NaN;

            marketDataItem = new MarketDataItem<OrderUpdate>
            {
                Timestamp = timestamp,
                Item = new OrderUpdate
                {
                    OrderId = orderId,
                    Side = side,
                    Price = price,
                    Quantity = quantity,
                    Type = type,
                    TradeId = tradeId,
                    ExecutionPrice = executionPrice
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