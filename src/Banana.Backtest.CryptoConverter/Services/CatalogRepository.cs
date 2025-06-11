using System.IO.Compression;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.CryptoConverter.Options;
using Banana.Backtest.CryptoConverter.Services.Models.Tardis;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Serilog.Events;
using Version = Banana.Backtest.Common.Models.Version;

namespace Banana.Backtest.CryptoConverter.Services;

public class CatalogRepository
{
    private readonly IMongoDatabase _database;
    private readonly ILogger _logger;
    private IMongoCollection<MarketDataCacheMetaPersistentModel> MetaCollection => _database.GetCollection<MarketDataCacheMetaPersistentModel>("cache-meta");
    private IMongoCollection<InstrumentInfo> InstrumentsCollection => _database.GetCollection<InstrumentInfo>("instruments");

    public CatalogRepository(IMongoClient mongoClient, IOptions<MongoOptions> options, ILogger logger)
    {
        _logger = logger.ForContext<CatalogRepository>();
        _database = mongoClient.GetDatabase(options.Value.DatabaseName);
        BsonSerializer.RegisterSerializer(SymbolSerializer.Instance);
        BsonSerializer.RegisterSerializer(VersionSerializer.Instance);
        BsonSerializer.RegisterSerializer(new EnumSerializer<CompressionType>(BsonType.String));
        BsonSerializer.RegisterSerializer(new EnumSerializer<CompressionLevel>(BsonType.String));
        BsonSerializer.RegisterSerializer(new EnumSerializer<FeedType>(BsonType.String));
        BsonClassMap.RegisterClassMap<MarketDataHash>(map =>
        {
            map.AutoMap();
        });
        BsonClassMap.RegisterClassMap<MarketDataCacheMetaPersistentModel>(map =>
        {
            map.AutoMap();
        });
        BsonClassMap.RegisterClassMap<InstrumentInfo>(map =>
        {
            map.AutoMap();
            map.MapIdProperty(x => x.Symbol);
        });
        MetaCollection.Indexes.CreateOne(new CreateIndexModel<MarketDataCacheMetaPersistentModel>(
            Builders<MarketDataCacheMetaPersistentModel>.IndexKeys
                .Ascending(x => x.Hash.Symbol)
                .Ascending(x => x.Hash.Date)
                .Ascending(x => x.Hash.Feed),
            new CreateIndexOptions
            {
                Unique = true,
                Background = true
            }
        ));
    }

    public async Task UpdateInstruments(IEnumerable<InstrumentInfo> instruments)
    {
        var updateModels = instruments
            .Select(instrument =>
            {
                var request = new ReplaceOneModel<InstrumentInfo>(
                    Builders<InstrumentInfo>.Filter.Eq(x => x.Symbol, instrument.Symbol),
                    instrument)
                {
                    IsUpsert = true
                };
                return request;
            });
        var result = await InstrumentsCollection.BulkWriteAsync(updateModels);
        if (result.IsAcknowledged)
            _logger.Debug("Updated instruments catalog with {Count} items", result.ModifiedCount);
    }

    public async IAsyncEnumerable<InstrumentInfo> GetInstruments(Exchange exchange)
    {
        using var cursor = await InstrumentsCollection.FindAsync(Builders<InstrumentInfo>.Filter.Empty);
        while (await cursor.MoveNextAsync())
        {
            foreach (var instrumentInfo in cursor.Current)
            {
                if (instrumentInfo.Symbol.Exchange == exchange)
                    yield return instrumentInfo;
            }
        }
    }

    public async Task<InstrumentInfo?> GetInstrument(Symbol symbol)
    {
        var filter = Builders<InstrumentInfo>.Filter.Eq(x => x.Symbol, symbol);
        var instrument = await InstrumentsCollection.Find(filter).FirstOrDefaultAsync();
        return instrument;
    }

    public async IAsyncEnumerable<MarketDataHash> GetCompleteMetaForSymbol(Symbol symbol)
    {
        var filter = Builders<MarketDataCacheMetaPersistentModel>.Filter.Eq(x => x.Hash.Symbol, symbol);
        using var cursor = await MetaCollection.FindAsync(filter);
        while (await cursor.MoveNextAsync())
        {
            foreach (var meta in cursor.Current)
            {
                yield return meta.Hash;
            }
        }
    }

    public async IAsyncEnumerable<MarketDataCacheMeta> GetAllMeta(IEnumerable<Exchange> exchanges)
    {
        var exchangeFilter = exchanges.Aggregate((flagsFilter, exchange) => flagsFilter | exchange);
        var filter = Builders<MarketDataCacheMetaPersistentModel>.Filter.Empty;
        using var cursor = await MetaCollection.FindAsync(filter);
        while (await cursor.MoveNextAsync())
        {
            foreach (var meta in cursor.Current)
            {
                var domainModel = meta.ToDomain();
                if ((exchangeFilter & domainModel.Hash.Symbol.Exchange) == domainModel.Hash.Symbol.Exchange)
                    yield return domainModel;
            }
        }
    }

    public async Task BuildComplete(MarketDataCacheMeta hash)
    {
        var filter = Builders<MarketDataCacheMetaPersistentModel>.Filter.And(
            Builders<MarketDataCacheMetaPersistentModel>.Filter.Eq(x => x.Hash.Symbol, hash.Hash.Symbol),
            Builders<MarketDataCacheMetaPersistentModel>.Filter.Eq(x => x.Hash.Date, hash.Hash.Date),
            Builders<MarketDataCacheMetaPersistentModel>.Filter.Eq(x => x.Hash.Feed, hash.Hash.Feed)
            );

        await MetaCollection.FindOneAndDeleteAsync(filter);
        await MetaCollection.InsertOneAsync(new MarketDataCacheMetaPersistentModel(hash));
    }

    public class VersionSerializer : SerializerBase<Version>
    {
        private static readonly Lock Lock = new();

        private VersionSerializer()
        {
        }

        public static VersionSerializer Instance
        {
            get
            {
                lock (Lock)
                {
                    return new VersionSerializer();
                }
            }
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Version value)
        {
            context.Writer.WriteString(value.ToString());
        }

        public override Version Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var stringValue = context.Reader.ReadString();
            return Version.Parse(stringValue);
        }
    }

    public class SymbolSerializer : SerializerBase<Symbol>
    {
        private static readonly Lock Lock = new();

        private SymbolSerializer()
        {
        }

        public static SymbolSerializer Instance
        {
            get
            {
                lock (Lock)
                {
                    return new SymbolSerializer();
                }
            }
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Symbol value)
        {
            var stringValue = value.ToString();
            context.Writer.WriteString(stringValue);
        }

        public override Symbol Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var stringValue = context.Reader.ReadString();
            return Symbol.Parse(stringValue);
        }
    }

    public struct MarketDataCacheMetaPersistentModel(MarketDataCacheMeta domainModel)
    {
        public MarketDataCacheMeta ToDomain()
        {
            return new MarketDataCacheMeta
            {
                Hash = Hash,
                CompressionType = CompressionType,
                CompressionLevel = CompressionLevel,
                ItemsCount = ItemsCount,
                BuildTime = BuildTime,
                Version = Version
            };
        }

        public ObjectId Id { get; private set; } = ObjectId.GenerateNewId();
        public MarketDataHash Hash { get; private set; } = domainModel.Hash;
        public CompressionType CompressionType { get; private set; } = domainModel.CompressionType;
        public CompressionLevel CompressionLevel { get; private set; } = domainModel.CompressionLevel;
        public long ItemsCount { get; private set; } = domainModel.ItemsCount;
        public DateTime BuildTime { get; private set; } = domainModel.BuildTime;
        public Version Version { get; private set; } = domainModel.Version;
    }
}
