using System.Globalization;
using Banana.Backtest.Common.Models.MarketData;

namespace Banana.Backtest.CryptoConverter.Parsers;

public class LevelUpdateLineParser : ILineParser<LevelUpdate>
{
    private const byte COMMA = 44; // ','

    public bool ParseLine(Span<byte> line, out MarketDataItem<LevelUpdate> marketDataItem)
    {
        marketDataItem = default;
        // exchange,symbol,timestamp,local_timestamp,is_snapshot,side,price,amount
        // binance-futures,1INCHUSDT,1713484803701000,1713484805318378,true,ask,0.4066,9513
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

        // is_snapshot
        indexOfComma = remainedLine.IndexOf(COMMA);
        if (indexOfComma == -1)
            return false;
        var isSnapshotSpan = remainedLine[..indexOfComma];
        var isSnapshot = isSnapshotSpan[0] == 't';
        remainedLine = remainedLine[(indexOfComma + 1)..];

        // side
        indexOfComma = remainedLine.IndexOf(COMMA);
        if (indexOfComma == -1)
            return false;
        var sideSpan = remainedLine[..indexOfComma];
        var isBid = sideSpan[0] == 'b';
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

        marketDataItem = new MarketDataItem<LevelUpdate>(new LevelUpdate
        {
            IsBid = isBid,
            IsSnapshot = isSnapshot,
            Price = price,
            Quantity = quantity
        },
            timestamp);

        return true;
    }
}
