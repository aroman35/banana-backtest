using Banana.Strategies.MeanReverse.Binance.Execution.Models;
using Binance.Net.Objects.Models.Futures.Socket;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Banana.Strategies.MeanReverse.Binance.Persistence;

public class MongoDbContext
{
    private readonly IMongoClient _mongoClient;
    private readonly ILogger _logger;

    private IMongoDatabase Database => _mongoClient.GetDatabase("mean-reverse-binance");

    public MongoDbContext(IMongoClient mongoClient, ILogger logger)
    {
        _mongoClient = mongoClient;
        _logger = logger.ForContext<MongoDbContext>();

        BsonSerializer.RegisterSerializer(GuidSerializer.StandardInstance.WithGuidRepresentation(GuidRepresentation.Standard));
        BsonSerializer.RegisterSerializer(DateTimeSerializer.UtcInstance);
        ConventionRegistry.Register("CamelCase", new ConventionPack
        {
            new CamelCaseElementNameConvention()
        }, _ => true);
        ConventionRegistry.Register("EnumStringConvention", new ConventionPack
        {
            new EnumRepresentationConvention(BsonType.String)
        }, _ => true);

        BsonClassMap.RegisterClassMap<ExecutionResult>(map =>
        {
            map.AutoMap();
            map.MapIdProperty(x => x.Id);
            map.UnmapProperty(x => x.PlacedOrders);
            map.UnmapProperty(x => x.TradesByClientOrderId);
            map.UnmapProperty(x => x.PendingOrders);
            map.UnmapMember(x => x.PlacedOrders);
            map.UnmapMember(x => x.TradesByClientOrderId);
            map.UnmapMember(x => x.PendingOrders);
        });
        BsonClassMap.RegisterClassMap<BinanceFuturesStreamOrderUpdateData>(map =>
        {
            map.AutoMap();
            map.MapIdProperty(x => x.OrderId);
        });
        BsonClassMap.RegisterClassMap<BinanceFuturesStreamTradeUpdate>(map =>
        {
            map.AutoMap();
            map.MapIdProperty(x => x.TradeId);
        });
    }

    public IMongoCollection<BinanceFuturesStreamOrderUpdateData> UserOrdersCollection => Database.GetCollection<BinanceFuturesStreamOrderUpdateData>("user-orders");

    public IMongoCollection<BinanceFuturesStreamTradeUpdate> UserTradesCollection => Database.GetCollection<BinanceFuturesStreamTradeUpdate>("user-trades");

    public IMongoCollection<ExecutionResult> ExecutionsCollection => Database.GetCollection<ExecutionResult>("executions");
}
