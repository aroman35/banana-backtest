using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.CryptoConverter.Services;
using FastEndpoints;

namespace Banana.Backtest.CryptoConverter.Endpoints.SymbolCacheInfoRequest;

public record SymbolCacheInfoQuery(string Symbol);

public record SymbolCacheInfoResponse(Symbol Symbol, DateOnly? StartDate, DateOnly? EndDate, int TotalLevelUpdateDays, int TotalTradeDates, Dictionary<FeedType, DateOnly[]> Details)
{
    public static SymbolCacheInfoResponse EmptyFor(Symbol symbol) => new SymbolCacheInfoResponse(symbol, null, null, 0, 0, []);
}

public class SymbolCacheInfoEndpoint(CatalogRepository catalogRepository) : Endpoint<SymbolCacheInfoQuery, SymbolCacheInfoResponse>
{
    public override void Configure()
    {
        Get("/info/{Symbol}");
        AllowAnonymous();
    }

    public override async Task<SymbolCacheInfoResponse> ExecuteAsync(SymbolCacheInfoQuery request, CancellationToken cancellationToken)
    {
        var symbol = Symbol.Parse(request.Symbol);

        var hashes = await catalogRepository
            .GetCompleteMetaForSymbol(symbol)
            .OrderByDescending(x => x.Date)
            .ToArrayAsync(cancellationToken: cancellationToken);

        if (hashes.Length == 0)
            return SymbolCacheInfoResponse.EmptyFor(symbol);
        
        var startDate = hashes[^1].Date;
        var endDate = hashes[0].Date;

        var details = hashes
            .GroupBy(x => x.Feed)
            .ToDictionary(
                x => x.Key,
                x => x.Select(hash => hash.Date).OrderByDescending(date => date).ToArray());

        details.TryGetValue(FeedType.LevelUpdates, out var levelUpdateDays);
        details.TryGetValue(FeedType.Trades, out var tradeDays);
        return new SymbolCacheInfoResponse(symbol, startDate, endDate, levelUpdateDays?.Length ?? 0, tradeDays?.Length ?? 0, details);
    }
}