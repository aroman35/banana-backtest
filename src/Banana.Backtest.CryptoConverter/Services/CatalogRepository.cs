using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.CryptoConverter.Converters;
using Banana.Backtest.CryptoConverter.Options;
using Banana.Backtest.CryptoConverter.Services.Models.Tardis;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using StackExchange.Redis;

namespace Banana.Backtest.CryptoConverter.Services;

public class CatalogRepository(IConnectionMultiplexer connectionMultiplexer, IOptions<RedisOptions> options, ILogger logger)
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase(options.Value.CatalogDatabase);
    private readonly ILogger _logger = logger.ForContext<CatalogRepository>();
    private readonly AsyncRetryPolicy _redisInsertRetryPolicy = Policy.Handle<Exception>()
        .WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        Converters =
        {
            new JsonStringEnumConverter(),
            SymbolSystemTextJsonConverter.Instance
        }
    };

    public async IAsyncEnumerable<MarketDataHash> GetCompleteMetaForSymbol(Symbol symbol)
    {
        var levelUpdatesHashes = await _database.SetMembersAsync(MarketDataHashKey(symbol, FeedType.LevelUpdates));
        var tradesHashes = await _database.SetMembersAsync(MarketDataHashKey(symbol, FeedType.Trades));

        foreach (var entry in levelUpdatesHashes)
        {
            var hash = ByteArrayToMeta(entry);
            yield return hash;
        }

        foreach (var entry in tradesHashes)
        {
            var hash = ByteArrayToMeta(entry);
            yield return hash;
        }
    }

    public async Task BuildComplete(MarketDataHash hash)
    {
        var hashSetKey = MarketDataHashKey(hash.Symbol, hash.Feed);
        var value = MetaToRedisValue(hash);

        var insertResult = await _redisInsertRetryPolicy.ExecuteAndCaptureAsync(() =>
            _database.SetAddAsync(hashSetKey, value));
        if (insertResult.Outcome == OutcomeType.Failure)
            _logger.Error(insertResult.FinalException, "Error while building catalog data for {Hash}", hash);
    }

    public async Task UpdateInstruments(Exchange exchange, IEnumerable<InstrumentInfo> instruments)
    {
        var hashEntries = instruments
            .Where(x => x.Symbol.Exchange == exchange)
            .Select(x => new HashEntry(x.Symbol.ToString(), JsonSerializer.Serialize(x, _jsonSerializerOptions)))
            .ToArray();
        await _database.HashSetAsync(InstrumentsKey(exchange), hashEntries);
    }

    public async IAsyncEnumerable<InstrumentInfo> GetInstruments(Exchange exchange)
    {
        var key = InstrumentsKey(exchange);
        var allInstrumentsKeys = await _database.HashGetAllAsync(key);
        foreach (var hashEntry in allInstrumentsKeys)
        {
            if (!hashEntry.Value.HasValue)
                continue;
            var instrumentInfo = JsonSerializer.Deserialize<InstrumentInfo>(hashEntry.Value!, _jsonSerializerOptions);
            if (instrumentInfo is not null)
                yield return instrumentInfo;
        }
    }

    public async Task<InstrumentInfo?> GetInstrument(Symbol symbol)
    {
        var key = InstrumentsKey(symbol.Exchange);
        var hash = new RedisValue(symbol.ToString());
        var result = await _database.HashGetAsync(key, hash);
        if (result.HasValue)
        {
            var instrumentInfo = JsonSerializer.Deserialize<InstrumentInfo>(
                result.ToString(),
                _jsonSerializerOptions);
            return instrumentInfo;
        }
        return default;
    }

    private static unsafe RedisValue MetaToRedisValue(MarketDataHash hash)
    {
        var buffer = MemoryPool<byte>.Shared.Rent(Unsafe.SizeOf<MarketDataHash>());
        buffer.Memory.Span.Clear();
        new Span<byte>(&hash, sizeof(MarketDataHash)).CopyTo(buffer.Memory.Span);
        RedisValue value = buffer.Memory[..Unsafe.SizeOf<MarketDataHash>()];
        return value;
    }

    private static unsafe MarketDataHash ByteArrayToMeta(ReadOnlyMemory<byte> metaRaw)
    {
        fixed (byte* rawPtr = metaRaw.Span)
        {
            return *(MarketDataHash*)rawPtr;
        }
    }

    private RedisKey MarketDataHashKey(Symbol symbol, FeedType feedType) =>
        new($"{options.Value.KeyPrefix}{symbol.Exchange}.{symbol.Ticker}.{symbol.ClassCode}.{feedType.ToString()}");

    private RedisKey InstrumentsKey(Exchange exchange) =>
        new($"{options.Value.KeyPrefix}instruments.{exchange.ToString()}");
}
