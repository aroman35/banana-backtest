using System.Collections.Concurrent;
using System.Threading.Channels;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;
using Serilog;
using Serilog.Events;

namespace Banana.Backtest.Emulator.Services;

public abstract class ExecutionBase<TExecutionSettings> :
    IChannelSubscriber<MarketDataItem>,
    IChannelSubscriber<UserExecution>,
    IChannelSubscriber<OrderInfo>
where TExecutionSettings : ExecutionSettings
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly TaskCompletionSource<ExecutionResult> _executionCompletion = new();
    private readonly ChannelReader<MarketDataItem> _marketDataFeed;
    private readonly ChannelReader<UserExecution> _executionsFeed;
    private readonly ChannelReader<OrderInfo> _orderInfoFeed;
    private readonly ChannelWriter<PlaceOrderRequest> _ordersFeed;
    private readonly ChannelWriter<CancelOrderRequest> _ordersCancellationFeed;

    private readonly Task _marketDataFeedTask;
    private readonly Task _executionsFeedTask;
    private readonly Task _orderInfoFeedTask;
    private readonly TimeProvider _timeProvider;

    private readonly HashSet<Guid> _clientOrderIds = new();
    private readonly HashSet<long> _exchangeOrderIds = new();

    private protected readonly TExecutionSettings Settings;
    private protected readonly ConcurrentDictionary<Guid, double> PlacedOrders = new();
    private protected readonly ConcurrentDictionary<long, Guid> ExchangeOrderIdsMap = new();
    private protected readonly ConcurrentDictionary<Guid, double> PendingPlaceOrders = new();
    private protected readonly ConcurrentDictionary<Guid, double> PendingCancelOrders = new();
    private protected readonly ConcurrentDictionary<Guid, double> FilledQuantities = new();
    private protected readonly ConcurrentDictionary<long, HashSet<UserExecution>> AllTradesByExchangeOrderId = new();

    private int _placedOrdersCount;
    private int _cancelledOrdersCount;

    private protected double LastPriceExecuted;
    private protected OrderBookSnapshot LastOrderBookSnapshot;
    private protected double FilledQuantity;
    private protected double PendingQuantity;

    protected ExecutionBase(
        IChannelsProvider channelsProvider,
        TimeProvider timeProvider,
        TExecutionSettings settings,
        ILogger logger)
    {
        Logger = logger.ForContext<ExecutionBase<TExecutionSettings>>();
        _timeProvider = timeProvider;
        Settings = settings;
        _marketDataFeed = channelsProvider.MarketDataCommonProviderChannel.Reader;
        _executionsFeed = channelsProvider.UserExecutionChannel.Reader;
        _orderInfoFeed = channelsProvider.OrderStatusesChannel.Reader;
        _ordersFeed = channelsProvider.UserOrdersChannel.Writer;
        _ordersCancellationFeed = channelsProvider.CancelOrdersChannel.Writer;
        _marketDataFeedTask = ((IChannelSubscriber<MarketDataItem>)this).SubscribeAsync(_cancellationTokenSource.Token);
        _executionsFeedTask = ((IChannelSubscriber<UserExecution>)this).SubscribeAsync(_cancellationTokenSource.Token);
        _orderInfoFeedTask = ((IChannelSubscriber<OrderInfo>)this).SubscribeAsync(_cancellationTokenSource.Token);
    }

    /// <summary>
    /// Логгер
    /// </summary>
    public ILogger Logger { get; }

    /// <summary>
    /// Ожидание результата выполнения
    /// </summary>
    public Task<ExecutionResult> ExecutionCompletion => _executionCompletion.Task;

    /// <summary>
    /// Получение пользовательской сделки
    /// </summary>
    /// <param name="channelData">Пользовательская сделка</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns></returns>
    public ValueTask HandleChannelDataAsync(UserExecution channelData, CancellationToken cancellationToken)
    {
        if (!_clientOrderIds.Contains(channelData.ClientOrderId))
            return ValueTask.CompletedTask;

        Logger.Information(
            "[{Time}] execution received: {Price}x{Quantity} ({ClientOrderId})",
            channelData.Timestamp.AsDateTime(),
            channelData.ExecutionPrice,
            channelData.ExecutedQuantity,
            channelData.ClientOrderId);

        var exchangeOrderId = channelData.OrderId;
        var tradesCollection = AllTradesByExchangeOrderId.GetOrAdd(exchangeOrderId, _ => []);
        if (tradesCollection.Add(channelData))
        {
            FilledQuantity += channelData.ExecutedQuantity;
            if (ExchangeOrderIdsMap.TryGetValue(exchangeOrderId, out var clientOrderId))
            {
                FilledQuantities[clientOrderId] += channelData.ExecutedQuantity;
                PendingQuantity -= channelData.ExecutedQuantity;
            }
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Получение обновления пользовательской заявки
    /// </summary>
    /// <param name="channelData">Обновленное состояние пользовательской заявки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns></returns>
    public ValueTask HandleChannelDataAsync(OrderInfo channelData, CancellationToken cancellationToken)
    {
        if (!_clientOrderIds.Contains(channelData.ClientOrderId))
            return ValueTask.CompletedTask;

        double orderPrice;
        switch (channelData.Status)
        {
            case OrderStatus.New:
                var isNewOrder = PendingPlaceOrders.TryRemove(channelData.ClientOrderId, out orderPrice) &&
                    PlacedOrders.TryAdd(channelData.ClientOrderId, orderPrice) &&
                    ExchangeOrderIdsMap.TryAdd(channelData.Id, channelData.ClientOrderId) &&
                    _exchangeOrderIds.Add(channelData.Id);
                if (isNewOrder)
                    _placedOrdersCount++;
                break;
            case OrderStatus.Cancelled:
                var isOrderCancelled = PendingPlaceOrders.TryRemove(channelData.ClientOrderId, out orderPrice) |
                    PlacedOrders.TryRemove(channelData.ClientOrderId, out orderPrice) &&
                    PendingCancelOrders.TryRemove(channelData.ClientOrderId, out orderPrice);
                if (isOrderCancelled)
                    _cancelledOrdersCount++;
                Logger.Debug(
                    "[{Time}] order {Price}x{Quantity} has been cancelled ({ClientOrderId})",
                    channelData.StatusUpdateTimestamp.AsDateTime(),
                    orderPrice,
                    channelData.RemainingQuantity,
                    channelData.ClientOrderId);
                break;
            case OrderStatus.PartiallyFill or OrderStatus.Fill:
                Logger.Debug(
                    "[{Time}] order ({ClientOrderId}) filled",
                    channelData.StatusUpdateTimestamp.AsDateTime(),
                    channelData.ClientOrderId);
                break;
            case OrderStatus.Rejected:
                _ = PendingPlaceOrders.TryRemove(channelData.ClientOrderId, out orderPrice) |
                    PlacedOrders.TryRemove(channelData.ClientOrderId, out orderPrice);
                Logger.Warning(
                    "[{Time}] order {Price}x{Quantity} has been rejected: {Reason} ({ClientOrderId})",
                    channelData.StatusUpdateTimestamp.AsDateTime(),
                    orderPrice,
                    channelData.RemainingQuantity,
                    channelData.RejectionReason,
                    channelData.ClientOrderId);
                break;
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Получение обновления рыночных данных
    /// </summary>
    /// <param name="channelData">Рыночные данные</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns></returns>
    public ValueTask HandleChannelDataAsync(MarketDataItem channelData, CancellationToken cancellationToken)
    {
        if (channelData.IsTrade)
        {
            LastPriceExecuted = channelData.Trade.Item.Price;
        }
        else
        {
            LastOrderBookSnapshot = channelData.OrderBook.Item;
        }

        return OnMarketDataUpdated();
    }

    /// <summary>
    /// Получено обновление рыночных данных
    /// </summary>
    /// <returns></returns>
    protected abstract ValueTask OnMarketDataUpdated();

    /// <summary>
    /// Получено обновление состояния
    /// </summary>
    /// <returns></returns>
    protected abstract ValueTask OnStateUpdated();

    /// <inheritdoc />
    ChannelReader<OrderInfo> IChannelSubscriber<OrderInfo>.Reader => _orderInfoFeed;

    /// <inheritdoc />
    ChannelReader<MarketDataItem> IChannelSubscriber<MarketDataItem>.Reader => _marketDataFeed;

    /// <inheritdoc />
    ChannelReader<UserExecution> IChannelSubscriber<UserExecution>.Reader => _executionsFeed;

    /// <summary>
    /// Выставление заявки
    /// </summary>
    /// <param name="order"></param>
    protected virtual async ValueTask<bool> PlaceOrder(PlaceOrderRequest order)
    {
        try
        {
            if (Logger.IsEnabled(LogEventLevel.Verbose))
            {
                Logger.Verbose(
                    "[{Time}]: preparing the {OrderType} order to be placed ({Side}) {Price}x{Quantity} [{ClientOrderId}]",
                    _timeProvider.GetUtcNow(),
                    order.OrderType,
                    order.Price,
                    order.Price,
                    order.Quantity,
                    order.ClientOrderId);
            }

            if (!_clientOrderIds.Add(order.ClientOrderId) ||
                !PendingPlaceOrders.TryAdd(order.ClientOrderId, order.Price) ||
                !FilledQuantities.TryAdd(order.ClientOrderId, 0.0D))
            {
                Logger.Warning(
                    "[{Time}]: Order {OrderType} cannot be placed ({Side}) {Price}x{Quantity} [{ClientOrderId}]",
                    _timeProvider.GetUtcNow(),
                    order.OrderType,
                    order.Side,
                    order.Price,
                    order.Quantity,
                    order.ClientOrderId);
                return false;
            }
            PendingQuantity += order.Quantity;
            await _ordersFeed.WriteAsync(order);
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug(
                    "[{Time}]: Placed {OrderType} order ({Side}) {Price}x{Quantity} [{ClientOrderId}]",
                    _timeProvider.GetUtcNow(),
                    order.OrderType,
                    order.Side,
                    order.Price,
                    order.Quantity,
                    order.ClientOrderId);
            }
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "[{Time}]: Order {OrderType} cannot be placed ({Side}) {Price}x{Quantity} [{ClientOrderId}]",
                _timeProvider.GetUtcNow(),
                order.OrderType,
                order.Side,
                order.Price,
                order.Quantity,
                order.ClientOrderId);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Отмена выставленной заявки
    /// </summary>
    /// <param name="order"></param>
    protected virtual async ValueTask CancelOrder(CancelOrderRequest order)
    {
        try
        {
            Logger.Verbose("Cancelling order {ClientOrderId}", order.ClientOrderId);
            if (PlacedOrders.TryGetValue(order.ClientOrderId, out var orderPrice)
                || PendingPlaceOrders.TryGetValue(order.ClientOrderId, out orderPrice))
            {
                if (PendingCancelOrders.TryAdd(order.ClientOrderId, orderPrice))
                {
                    await _ordersCancellationFeed.WriteAsync(order);
                    Logger.Debug("Order {ClientOrderId} cancelled", order.ClientOrderId);
                }
                else
                {
                    Logger.Warning("Order {ClientOrderId} is already pending cancel", order.ClientOrderId);
                }
            }
            else
            {
                Logger.Warning("Order {ClientOrderId} cannot be cancelled as it doesn't exists", order.ClientOrderId);
            }
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Error while cancelling order");
            throw;
        }
    }

    protected virtual void ExecutionCompleted()
    {
        var executionResult = new ExecutionResult
        {
            TradesCount = AllTradesByExchangeOrderId.Values.Sum(x => x.Count),
            PlacedOrdersCount = _placedOrdersCount,
            CancelledOrdersCount = _cancelledOrdersCount,
            ExecutedQuantity = FilledQuantity,
            MeanExecutionPrice = AllTradesByExchangeOrderId.Values.Average(x => x.Average(t => t.ExecutionPrice))
        };
        _executionCompletion.SetResult(executionResult);
        _cancellationTokenSource.Cancel();
    }

    /// <summary>
    /// Ожидание завершения всех операций перед закрытием
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // await Task.WhenAll(
        //     _marketDataFeedTask,
        //     _executionsFeedTask,
        //     _orderInfoFeedTask);

        Logger.Debug("Disposed");
    }
}
