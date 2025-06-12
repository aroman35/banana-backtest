using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.Crypto.Core.Catalog.Models.Tardis;

namespace Banana.Backtest.Crypto.Core.Abstractions;

public interface ICatalogue
{
    Task UpdateInstruments(IEnumerable<InstrumentInfo> instruments);
    IAsyncEnumerable<InstrumentInfo> GetInstruments(Exchange exchange);
    Task<InstrumentInfo?> GetInstrument(Symbol symbol);
    IAsyncEnumerable<MarketDataHash> GetCompleteMetaForSymbol(Symbol symbol);
    IAsyncEnumerable<MarketDataCacheMeta> GetAllMeta(IEnumerable<Exchange> exchanges);
    Task BuildComplete(MarketDataCacheMeta hash);
}
