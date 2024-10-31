using System.Globalization;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;

namespace Banana.Backtest.CryptoConverter.Parsers;

public class TradeUpdateLineParser : ILineParser<TradeUpdate>
{
    private const byte COMMA = 44; // ','
    public bool ParseLine(Span<byte> line, out MarketDataItem<TradeUpdate> marketDataItem)
    {
        //exchange,symbol,timestamp,local_timestamp,id,side,price,amount
        //binance-futures,1INCHUSDT,1704499202812000,1704499202817003,280059043,buy,0.4835,207
        marketDataItem = default;
        // exchange
        var indexOfComma = line.IndexOf(COMMA);
        if (indexOfComma == -1)
            return false;
        var remainedLine = line[(indexOfComma + 1)..];

        // symbol
        indexOfComma = remainedLine.IndexOf(COMMA);
        if (indexOfComma == -1)
            return false;
        remainedLine = remainedLine[(indexOfComma + 1)..];

        // timestamp
        indexOfComma = remainedLine.IndexOf(COMMA);
        if (indexOfComma == -1)
            return false;
        var timestampSpan = remainedLine[..indexOfComma];
        if (!long.TryParse(timestampSpan, CultureInfo.InvariantCulture, out var timestamp))
            return false;
        timestamp /= 1000;
        remainedLine = remainedLine[(indexOfComma + 1)..];

        // local_timestamp
        indexOfComma = remainedLine.IndexOf(COMMA);
        if (indexOfComma == -1)
            return false;
        remainedLine = remainedLine[(indexOfComma + 1)..];

        // id
        indexOfComma = remainedLine.IndexOf(COMMA);
        if (indexOfComma == -1)
            return false;
        var idSpan = remainedLine[..indexOfComma];
        if (!long.TryParse(idSpan, CultureInfo.InvariantCulture, out var id))
            return false;
        remainedLine = remainedLine[(indexOfComma + 1)..];

        // side
        indexOfComma = remainedLine.IndexOf(COMMA);
        if (indexOfComma == -1)
            return false;
        var sideSpan = remainedLine[..indexOfComma];
        var side = sideSpan[0] == 'b' ? Side.Long : Side.Short;
        remainedLine = remainedLine[(indexOfComma + 1)..];

        // price
        indexOfComma = remainedLine.IndexOf(COMMA);
        if (indexOfComma == -1)
            return false;
        var priceSpan = remainedLine[..indexOfComma];
        if (!double.TryParse(priceSpan, CultureInfo.InvariantCulture, out var price))
            return false;
        remainedLine = remainedLine[(indexOfComma + 1)..];

        // amount
        if (!double.TryParse(remainedLine, CultureInfo.InvariantCulture, out var quantity))
            return false;

        marketDataItem = new MarketDataItem<TradeUpdate>(new TradeUpdate
        {
            Price = price,
            Quantity = quantity,
            Side = side,
            TradeId = id
        },
            timestamp);

        return true;
    }
}
