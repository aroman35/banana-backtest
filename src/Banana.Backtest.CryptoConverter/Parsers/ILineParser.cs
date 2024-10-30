using Banana.Backtest.Common.Models.MarketData;

namespace Banana.Backtest.CryptoConverter.Parsers;

public interface ILineParser<TMarketDataType>
    where TMarketDataType : unmanaged
{
    bool ParseLine(Span<byte> line, out MarketDataItem<TMarketDataType> marketDataItem);
}