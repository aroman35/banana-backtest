using System.Linq.Expressions;
using Banana.Backtest.Common.Extensions;
using Banana.Strategies.MeanReverse.Binance.DataFlow;
using Banana.Strategies.MeanReverse.Binance.Execution.Models;
using Binance.Net.Objects.Models.Futures.Socket;
using MongoDB.Driver;
using static Banana.Strategies.MeanReverse.Binance.Extensions.JobsExtensions;

namespace Banana.Strategies.MeanReverse.Binance.Persistence;

public class UserDataSaverService(DataFlowMediator mediator, MongoDbContext mongoContext, ILogger logger)
    : BackgroundService
{
    private readonly ILogger _logger = logger.ForContext<UserDataSaverService>();

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.WhenAll(
            HandleStreamData(
                mediator.Stream<ExecutionResult>(stoppingToken),
                (data, ctx) => UpsertOne(mongoContext.ExecutionsCollection, data, x => x.Id, ctx),
                exception =>
                {
                    _logger.Error(exception, "Error in data saver of type {Type}", Helpers.FriendlyTypeName<ExecutionResult>());
                    return Task.CompletedTask;
                },
                stoppingToken),
            HandleStreamData(
                mediator.Stream<BinanceFuturesStreamTradeUpdate>(stoppingToken),
                (data, ctx) => UpsertOne(mongoContext.UserTradesCollection, data, x => x.TradeId, ctx),
                exception =>
                {
                    _logger.Error(exception, "Error in data saver of type {Type}", Helpers.FriendlyTypeName<BinanceFuturesStreamTradeUpdate>());
                    return Task.CompletedTask;
                },
                stoppingToken),
            HandleStreamData(
                mediator.Stream<BinanceFuturesStreamOrderUpdate>(stoppingToken),
                (data, ctx) => UpsertOne(mongoContext.UserOrdersCollection, data.UpdateData, x => x.OrderId, ctx),
                exception =>
                {
                    _logger.Error(exception, "Error in data saver of type {Type}", Helpers.FriendlyTypeName<BinanceFuturesStreamOrderUpdate>());
                    return Task.CompletedTask;
                },
                stoppingToken));
    }

    private static async ValueTask UpsertOne<T, TIdField>(IMongoCollection<T> collection, T item, Expression<Func<T, TIdField>> fieldDefinition, CancellationToken cancellationToken = default)
    {
        var idSelector = fieldDefinition.Compile();
        var filter = Builders<T>.Filter.Eq(fieldDefinition, idSelector(item));
        await collection.FindOneAndReplaceAsync(filter, item, new FindOneAndReplaceOptions<T> { IsUpsert = true }, cancellationToken);
    }
}
