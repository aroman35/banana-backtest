using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.Common.Services;

public interface IParserHandler<T> : IDisposable
    where T : unmanaged
{
    void Handle(MarketDataItem<T> marketDataItem, Symbol ticker);
}