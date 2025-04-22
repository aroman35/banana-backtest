using System.Collections.Concurrent;
using System.Threading.Channels;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;
using Banana.Backtest.Emulator.ExchangeEmulator;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

namespace Banana.Backtest.Emulator.Services;

/// <summary>
/// Матчер для одного инструмента
/// </summary>
public class BacktestMatcher :
    IChannelSubscriber<MarketDataItem<TradeUpdate>>,
    IChannelSubscriber<MarketDataItem<LevelUpdate>>,
    IChannelSubscriber<PlaceOrderRequest>,
    IChannelSubscriber<CancelOrderRequest>
{
    private readonly OrderBook _orderBook = new();
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<Guid, OrderInfo> _userOrdersStorage = new();

    private readonly SortedDictionary<double, HashSet<Guid>> _userBids =
        new(Comparer<double>.Create((x, y) => y.CompareTo(x)));

    private readonly SortedDictionary<double, HashSet<Guid>> _userAsks =
        new(Comparer<double>.Create((x, y) => x.CompareTo(y)));

    private readonly ConcurrentQueue<MarketDataItem<TradeUpdate>> _tradesCache = new();

    #region channels

    private readonly ChannelReader<MarketDataItem<TradeUpdate>> _sourceTradesFeed;
    private readonly ChannelReader<MarketDataItem<LevelUpdate>> _sourceLevelUpdatesFeed;
    private readonly ChannelReader<PlaceOrderRequest> _userOrdersFeed;
    private readonly ChannelReader<CancelOrderRequest> _userOrdersCancellationFeed;
    private readonly ChannelWriter<UserExecution> _userExecutionsFeed;
    private readonly ChannelWriter<OrderInfo> _orderStatusesFeed;
    private readonly ChannelWriter<MarketDataItem> _marketDataFeed;

    private readonly Task _tradesFeedTask;
    private readonly Task _levelUpdatesFeedTask;
    private readonly Task _userOrdersFeedTask;
    private readonly Task _userOrdersCancellationFeedTask;

    private readonly IOptions<MatcherSettings> _settings;

    #endregion

    private long _lastOrderBookUpdateTs;
    private int _levelUpdatesCount;
    private int _tradesCount;
    private int _orderBooksCount;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="channelsProvider">Поставщик подписок</param>
    /// <param name="timeProvider">Часы</param>
    /// <param name="settings">Настройки матчера</param>
    /// <param name="logger">Логгер</param>
    public BacktestMatcher(
        IChannelsProvider channelsProvider,
        TimeProvider timeProvider,
        IOptions<MatcherSettings> settings,
        ILogger logger)
    {
        Logger = logger.ForContext<BacktestMatcher>();
        _sourceTradesFeed = channelsProvider.GetMarketDataSourceChannel<TradeUpdate>().Reader;
        _sourceLevelUpdatesFeed = channelsProvider.GetMarketDataSourceChannel<LevelUpdate>().Reader;
        _userOrdersCancellationFeed = channelsProvider.CancelOrdersChannel.Reader;
        _userOrdersFeed = channelsProvider.UserOrdersChannel.Reader;
        _userExecutionsFeed = channelsProvider.UserExecutionChannel.Writer;
        _orderStatusesFeed = channelsProvider.OrderStatusesChannel.Writer;
        _marketDataFeed = channelsProvider.MarketDataCommonProviderChannel.Writer;
        _tradesFeedTask = ((IChannelSubscriber<MarketDataItem<TradeUpdate>>)this).SubscribeAsync();
        _levelUpdatesFeedTask = ((IChannelSubscriber<MarketDataItem<LevelUpdate>>)this).SubscribeAsync();
        _userOrdersFeedTask = ((IChannelSubscriber<PlaceOrderRequest>)this).SubscribeAsync();
        _userOrdersCancellationFeedTask = ((IChannelSubscriber<CancelOrderRequest>)this).SubscribeAsync();
        _timeProvider = timeProvider;
        _settings = settings;
    }


    /// <inheritdoc cref="IChannelSubscriber{TChannelData}.Logger" />
    public ILogger Logger { get; }

    /// <summary>
    /// Отмена пользовательской заявки
    /// </summary>
    /// <param name="channelData">Команда на удаление</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns></returns>
    public async ValueTask HandleChannelDataAsync(CancelOrderRequest channelData, CancellationToken cancellationToken)
    {
        if (_userOrdersStorage.TryGetValue(channelData.ClientOrderId, out var order))
        {
            order = order.Cancel(_timeProvider.GetTimestamp());
            await UpdateOrder(order, UserExecution.None, cancellationToken);
        }
    }

    /// <summary>
    /// Получение обновления уровня стакана
    /// </summary>
    /// <param name="channelData">Обновление уровня стакана</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public async ValueTask HandleChannelDataAsync(
        MarketDataItem<LevelUpdate> channelData,
        CancellationToken cancellationToken)
    {
        ++_levelUpdatesCount;
        var userOrdersCollection = channelData.Item.IsBid ? _userBids : _userAsks;
        var userQuantity = 0.0D;
        if (userOrdersCollection.TryGetValue(channelData.Item.Price, out var ordersIdx))
        {
            foreach (var clientOrderId in ordersIdx)
            {
                if (_userOrdersStorage.TryGetValue(clientOrderId, out var userOrder))
                {
                    userQuantity += userOrder.Price;
                }
            }
        }

        if (!userQuantity.IsEquals(0.0D))
        {
            channelData = channelData with
            {
                Item = new LevelUpdate
                {
                    Quantity = channelData.Item.Quantity + userQuantity
                }
            };
        }

        var bestBid = _orderBook.BestBid;
        var bestAsk = _orderBook.BestAsk;
        _orderBook.UpdateOrder(channelData);
        var orderBookUpdated = _lastOrderBookUpdateTs != channelData.Timestamp;
        _lastOrderBookUpdateTs = channelData.Timestamp;
        // TODO: Отправить обновление только если изменения произошли на доступных уровнях!!!
        if (orderBookUpdated)
        {
            _orderBooksCount++;
            // Если дельта цен обновилась, то топ уровни поменялись
            if (!bestBid().Price.IsEquals(_orderBook.BestBid().Price))
                await ExecuteUserOrdersMatching(Side.Short, cancellationToken);

            if (!bestAsk().Price.IsEquals(_orderBook.BestAsk().Price))
                await ExecuteUserOrdersMatching(Side.Long, cancellationToken);

            // Прежде чем отправлять следующий трейд надо убедиться что все предыдущие буки обработались
            while (_tradesCache.TryPeek(out var trade) && trade.Timestamp <= channelData.Timestamp)
            {
                await _marketDataFeed.WaitToWriteAsync(cancellationToken);
                await _marketDataFeed.WriteAsync(MarketDataItem.FromTrade(trade), cancellationToken);
                _tradesCache.TryDequeue(out _);
            }

            await _marketDataFeed.WaitToWriteAsync(cancellationToken);
            await _marketDataFeed.WriteAsync(MarketDataItem.FromOrderBook(_orderBook.TakeSnapshot()), cancellationToken);
        }
    }

    /// <summary>
    /// Получение обезличенной сделки
    /// </summary>
    /// <param name="channelData">Обезличенная сделка</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns></returns>
    public ValueTask HandleChannelDataAsync(
        MarketDataItem<TradeUpdate> channelData,
        CancellationToken cancellationToken)
    {
        ++_tradesCount;
        _tradesCache.Enqueue(channelData);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Получение запроса пользователя на выставление заявки
    /// </summary>
    /// <param name="channelData">Запрос пользователя на выставление заявки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public async ValueTask HandleChannelDataAsync(PlaceOrderRequest channelData, CancellationToken cancellationToken)
    {
        if (!ValidateOrder(ref channelData, out var reason))
        {
            // Reject
            Logger.Debug("Order {ClientOrderId} rejected: {Reason}", channelData.ClientOrderId, reason);
            await _orderStatusesFeed.WriteAsync(
                OrderInfo.Reject(channelData, _timeProvider.GetTimestamp(), reason),
                cancellationToken);
        }

        var order = OrderInfo.Accept(channelData, _timeProvider.GetTimestamp());

        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug(
                "Order ({ClientOrderId}) {Side} accepted {Price}x{Quantity}/{RequestedQuantity}",
                order.ClientOrderId,
                order.Side,
                order.Price,
                order.ExecutedQuantity,
                order.RequestedQuantity);
        }

        if (order.Type is OrderType.Market)
        {
            await HandleMarketOrder(order, cancellationToken);
        }

        if (order.Type is OrderType.Limit)
        {
            await HandleLimitOrder(order, cancellationToken);
        }
    }

    /// <summary>
    /// Валидация запроса пользователя на выставление заявки
    /// </summary>
    /// <param name="order">Запрос пользователя на выставление заявки</param>
    /// <param name="reason"></param>
    /// <returns></returns>
    private bool ValidateOrder(ref PlaceOrderRequest order, out RejectionReason reason)
    {
        reason = RejectionReason.None;

        if (_userOrdersStorage.ContainsKey(order.ClientOrderId))
        {
            reason = RejectionReason.DuplicateClientOrderId;
            return false;
        }

        if (order.Side is Side.Undefined)
        {
            reason = RejectionReason.UndefinedSide;
            return false;
        }

        if (order.Quantity.IsLowerOrEquals(0.0D) || double.IsNaN(order.Quantity))
        {
            reason = RejectionReason.NegativeOrZeroQuantity;
            return false;
        }

        if (order.OrderType is OrderType.Limit && (order.Price.IsLowerOrEquals(0.0D) || double.IsNaN(order.Price)))
        {
            reason = RejectionReason.NegativeOrZeroPrice;
            return false;
        }

        if (order.OrderType is OrderType.Limit && !(order.Price / _settings.Value.PriceStep % 1).IsLowerOrEquals(0.0D))
        {
            reason = RejectionReason.PriceStepDecreased;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Обновление состояния заявки с отправкой сопутствующих событий
    /// </summary>
    /// <param name="order">Обновленное состояние заявки</param>
    /// <param name="execution">Сделка полученная при исполнении заявки (при наличии)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    private async ValueTask UpdateOrder(OrderInfo order, UserExecution execution, CancellationToken cancellationToken)
    {
        if (order.Type is not OrderType.Limit)
            return;

        _userOrdersStorage[order.ClientOrderId] = order;
        var ordersCollection = order.Side is Side.Long ? _userBids : _userAsks;
        var orderBookLevelsCollection = order.Side is Side.Long ? _orderBook.Bids : _orderBook.Ask;
        orderBookLevelsCollection.TryGetValue(order.Price, out var orderBookLevel);
        if (!ordersCollection.TryGetValue(order.Price, out var ordersIndex))
        {
            ordersIndex = [];
            ordersCollection.TryAdd(order.Price, ordersIndex);
        }

        if (order.Status is OrderStatus.New)
        {
            ordersIndex.Add(order.ClientOrderId);
            var levelUpdate = new MarketDataItem<LevelUpdate>(new LevelUpdate
            {
                IsBid = order.Side is Side.Long,
                IsSnapshot = false,
                Price = order.Price,
                Quantity = orderBookLevel
                    .Quantity, // + order.RequestedQuantity => объем юзера дозапишется при обновлении уровня
            }, _timeProvider.GetTimestamp());
            await HandleChannelDataAsync(levelUpdate, cancellationToken);
        }

        if (order.Status is OrderStatus.Fill or OrderStatus.Cancelled)
        {
            if (ordersIndex.Remove(order.ClientOrderId))
            {
                if (ordersIndex.Count == 0)
                {
                    ordersCollection.Remove(order.Price);
                }
            }

            var replacedQuantity = order.Status is OrderStatus.Cancelled
                ? order.RemainingQuantity
                : execution.ExecutedQuantity;
            var levelUpdate = new MarketDataItem<LevelUpdate>(new LevelUpdate
            {
                IsBid = order.Side is Side.Long,
                IsSnapshot = false,
                Price = order.Price,
                Quantity = orderBookLevel.Quantity - replacedQuantity
            }, _timeProvider.GetTimestamp());
            await HandleChannelDataAsync(levelUpdate, cancellationToken);
        }

        Logger.Debug("Updating order {ClientOrderId}: {Status}", order.ClientOrderId, order.Status);
        await _orderStatusesFeed.WriteAsync(order, cancellationToken);
    }

    /// <summary>
    /// Обработка и исполнение рыночной заявки
    /// </summary>
    /// <param name="order">Состояние заявки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    private async ValueTask HandleMarketOrder(OrderInfo order, CancellationToken cancellationToken)
    {
        var bestOffer = _orderBook.BestOffer(order.Side);
        while (bestOffer.Quantity.IsGreater(0) && order.Status is not OrderStatus.Fill)
        {
            var executedQuantity = Math.Min(order.RemainingQuantity, bestOffer.Quantity);
            var execution = UserExecution.FillOrder(ref order, bestOffer.Price, executedQuantity,
                _timeProvider.GetTimestamp());
            await OrderExecuted(order, execution, cancellationToken);
        }

        if (order.Status is not OrderStatus.Fill)
        {
            await _orderStatusesFeed.WriteAsync(
                order.Reject(_timeProvider.GetTimestamp(), RejectionReason.NotEnoughLiquidity), cancellationToken);
        }
    }

    /// <summary>
    /// Обработка и исполнение лимитной заявки
    /// </summary>
    /// <param name="order">Состояние заявки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    private async ValueTask HandleLimitOrder(OrderInfo order, CancellationToken cancellationToken)
    {
        var bestOffer = _orderBook.BestOffer(order.Side);

        // Часть лимитки может исполниться как Taker
        if ((order.Price - bestOffer.Price * (int)order.Side).IsGreaterOrEquals(0.0D))
        {
            var executedQuantity = Math.Min(order.RemainingQuantity, bestOffer.Quantity);
            var execution = UserExecution.FillOrder(ref order, bestOffer.Price, executedQuantity, _timeProvider.GetTimestamp());
            await OrderExecuted(order, execution, cancellationToken);
        }

        await UpdateOrder(order, UserExecution.None, cancellationToken);
    }

    /// <summary>
    /// Процесс сопоставления выставленных пользовательских лимитных заявок наличию ликвидности в стакане
    /// </summary>
    /// <param name="side">Направление сопоставления</param>
    /// <param name="cancellationToken">Токен отмены</param>
    private async ValueTask ExecuteUserOrdersMatching(Side side, CancellationToken cancellationToken)
    {
        var ordersCollection = side is Side.Long ? _userBids : _userAsks;
        var userPrices = ordersCollection.Keys;
        foreach (var userPrice in userPrices)
        {
            var bestOffer = _orderBook.BestOffer(Side.Long);
            if ((userPrice - bestOffer.Price * (int)side).IsGreaterOrEquals(0.0D))
            {
                var userOrdersIndex = ordersCollection[userPrice];
                if (userOrdersIndex.Count == 0)
                    continue;
                foreach (var clientOrderId in userOrdersIndex)
                {
                    if (_userOrdersStorage.TryGetValue(clientOrderId, out var order))
                    {
                        var executedQuantity = Math.Min(order.RemainingQuantity, bestOffer.Quantity);
                        var execution = UserExecution.FillOrder(ref order, bestOffer.Price, executedQuantity, _timeProvider.GetTimestamp(), true);
                        await OrderExecuted(order, execution, cancellationToken);
                    }
                }
            }
            else
            {
                return;
            }
        }
    }

    /// <summary>
    /// Исполнение пользовательской заявки
    /// </summary>
    /// <param name="order">Состояние заявки</param>
    /// <param name="execution">Полученная пользовательская сделка</param>
    /// <param name="cancellationToken">Токен отмены</param>
    private async ValueTask OrderExecuted(OrderInfo order, UserExecution execution, CancellationToken cancellationToken)
    {
        await _orderStatusesFeed.WriteAsync(order, cancellationToken);
        await _userExecutionsFeed.WriteAsync(execution, cancellationToken);
        // await _marketDataFeed.WriteAsync(MarketDataItem.FromTrade(execution.ToMarketDataItem()), cancellationToken);

        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug(
                "Order ({ClientOrderId}) {Side} executed {Price}x{Quantity}/{RequestedQuantity}",
                order.ClientOrderId,
                order.Side,
                order.Price,
                order.ExecutedQuantity,
                order.RequestedQuantity);
        }

        if (order.Type is OrderType.Limit)
        {
            await UpdateOrder(order, execution, cancellationToken);
        }
    }

    #region Channels

    /// <inheritdoc />
    ChannelReader<MarketDataItem<LevelUpdate>> IChannelSubscriber<MarketDataItem<LevelUpdate>>.Reader =>
        _sourceLevelUpdatesFeed;

    /// <inheritdoc />
    ChannelReader<MarketDataItem<TradeUpdate>> IChannelSubscriber<MarketDataItem<TradeUpdate>>.Reader =>
        _sourceTradesFeed;

    /// <inheritdoc />
    ChannelReader<CancelOrderRequest> IChannelSubscriber<CancelOrderRequest>.Reader => _userOrdersCancellationFeed;

    /// <inheritdoc />
    ChannelReader<PlaceOrderRequest> IChannelSubscriber<PlaceOrderRequest>.Reader => _userOrdersFeed;

    #endregion

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Сначала дожидаемся окончания обработки и только после этого отписываемся сами
        await Task.WhenAll(
            Task.Run(async () => _ = await _userExecutionsFeed.WaitToWriteAsync() && _userExecutionsFeed.TryComplete()),
            Task.Run(async () => _ = await _orderStatusesFeed.WaitToWriteAsync() && _orderStatusesFeed.TryComplete()),
            Task.Run(async () => _ = await _marketDataFeed.WaitToWriteAsync() && _marketDataFeed.TryComplete())
            );
        await Task.WhenAll(
            _sourceTradesFeed.Completion,
            _sourceLevelUpdatesFeed.Completion,
            _userOrdersFeed.Completion,
            _userOrdersCancellationFeed.Completion
            );
        await Task.WhenAll(
            _tradesFeedTask,
            _levelUpdatesFeedTask,
            _userOrdersFeedTask,
            _userOrdersCancellationFeedTask
            );
        Logger.Information("Trades count: {TradesCount} level updates count {LevelUpdatesCount} order-books count: {OrderBooksCount}", _tradesCount, _levelUpdatesCount, _orderBooksCount);
        Logger.Debug("Disposed");
    }
}
