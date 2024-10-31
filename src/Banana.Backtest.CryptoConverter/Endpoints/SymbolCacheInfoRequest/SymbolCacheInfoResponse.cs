using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.CryptoConverter.Endpoints.SymbolCacheInfoRequest;

public record SymbolCacheInfoResponse(Symbol Symbol, DateOnly? StartDate, DateOnly? EndDate, int TotalLevelUpdateDays, int TotalTradeDates, Dictionary<FeedType, DateOnly[]> Details)
{
    public static SymbolCacheInfoResponse EmptyFor(Symbol symbol) => new SymbolCacheInfoResponse(symbol, null, null, 0, 0, []);
}