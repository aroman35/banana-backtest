using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.Root;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Banana.Backtest.Launcher.Cache.Catalog;

public class InstrumentsCatalog(IDatabase database, IOptions<RedisOptions> options, ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<InstrumentsCatalog>();
    private readonly ConcurrentDictionary<Asset, RedisKey> _instrumentsKeys = new();

    public async IAsyncEnumerable<FuturesInstrument> FuturesByBasicAsset(Asset asset, DateOnly startDate, DateOnly endDate)
    {
        var key = _instrumentsKeys.GetOrAdd(asset, FuturesDataHashKey);
        var futuresResponse = await database.HashGetAllAsync(key);
        foreach (var hashEntry in futuresResponse)
        {
            var expirationDate = DateOnly.Parse(hashEntry.Name!);
            if (expirationDate < startDate)
                continue;
            var futuresInfo = ByteArrayToFuturesInstrument(hashEntry.Value);
            if (futuresInfo.FirstTradeDate > endDate)
                continue;
            yield return futuresInfo;
        }
    }

    public async Task<Symbol> GetSymbolForAsset(DateOnly dateOnly, Asset asset)
    {
        var key = _instrumentsKeys.GetOrAdd(asset, FuturesDataHashKey);
        var futuresResponse = await database.HashGetAllAsync(key);
        var symbol = futuresResponse
            .Select(x => ByteArrayToFuturesInstrument(x.Value))
            .Where(x => x.ExpirationDate > dateOnly)
            .OrderBy(x => x.ExpirationDate)
            .Select(x => x.Symbol)
            .FirstOrDefault();
        return symbol;
    }

    public async Task SetInstruments(IEnumerable<FuturesInstrument> instruments)
    {
        var instrumentsByBasicAsset = instruments.GroupBy(x => x.BasicAsset);
        foreach (var instrumentsGroup in instrumentsByBasicAsset)
        {
            try
            {
                var key = _instrumentsKeys.GetOrAdd(instrumentsGroup.Key, FuturesDataHashKey);
                var hashEntries = instrumentsGroup
                    .OrderByDescending(x => x.ExpirationDate)
                    .Select(x => new HashEntry(x.ExpirationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        FuturesInstrumentToRedisValue(x)))
                    .ToArray();
                await database.HashSetAsync(key, hashEntries);
                _logger.Information("Upgraded {Count} futures for {BasicAsset}", hashEntries.Length,
                    instrumentsGroup.Key);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Failed to insert futures for {BasicAsset}", instrumentsGroup.Key);
            }
        }
    }

    private static unsafe FuturesInstrument ByteArrayToFuturesInstrument(ReadOnlyMemory<byte> bytes)
    {
        fixed (byte* ptr = bytes.Span)
        {
            return *(FuturesInstrument*)ptr;
        }
    }

    private static unsafe RedisValue FuturesInstrumentToRedisValue(FuturesInstrument instrument)
    {
        var buffer = MemoryPool<byte>.Shared.Rent(sizeof(FuturesInstrument));
        buffer.Memory.Span.Clear();
        new Span<byte>(&instrument, sizeof(FuturesInstrument)).CopyTo(buffer.Memory.Span);
        RedisValue value = buffer.Memory[..sizeof(FuturesInstrument)];
        return value;
    }

    private RedisKey FuturesDataHashKey(Asset asset) => new($"{options.Value.KeyPrefix}instruments.futures.{asset.ToString()}");
}
