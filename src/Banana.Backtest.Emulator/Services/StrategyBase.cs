using System.Threading.Channels;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;
using Serilog;
using Serilog.Events;

namespace Banana.Backtest.Emulator.Services;

/// <summary>
/// Базовый класс для стратегий со всеми необходимыми подписками
/// TODO: Добавить метод для отмены заявок
/// </summary>
public abstract class StrategyBase :
    IChannelSubscriber<MarketDataItem>,
    IChannelSubscriber<UserExecution>,
    IChannelSubscriber<OrderInfo>
{
    private readonly IChannelsProvider _channelsProvider;
    private readonly ChannelReader<MarketDataItem> _marketDataFeed;
    private readonly ChannelReader<UserExecution> _executionsFeed;
    private readonly ChannelReader<OrderInfo> _orderInfoFeed;
    private readonly ChannelWriter<PlaceOrderRequest> _ordersFeed;
    private readonly ChannelWriter<CancelOrderRequest> _ordersCancellationFeed;

    private readonly Task _marketDataFeedTask;
    private readonly Task _executionsFeedTask;
    private readonly Task _orderInfoFeedTask;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="channelsProvider">Поставщик подписок</param>
    /// <param name="timeProvider">Часы</param>
    /// <param name="logger">Логгер</param>
    protected StrategyBase(
        IChannelsProvider channelsProvider,
        TimeProvider timeProvider,
        ILogger logger)
    {
        Logger = logger.ForContext<StrategyBase>();
        _channelsProvider = channelsProvider;
        _timeProvider = timeProvider;
        _marketDataFeed = channelsProvider.MarketDataCommonProviderChannel.Reader;
        _executionsFeed = channelsProvider.UserExecutionChannel.Reader;
        _orderInfoFeed = channelsProvider.OrderStatusesChannel.Reader;
        _ordersFeed = channelsProvider.UserOrdersChannel.Writer;
        _ordersCancellationFeed = channelsProvider.CancelOrdersChannel.Writer;
        _marketDataFeedTask = ((IChannelSubscriber<MarketDataItem>)this).SubscribeAsync();
        _executionsFeedTask = ((IChannelSubscriber<UserExecution>)this).SubscribeAsync();
        _orderInfoFeedTask = ((IChannelSubscriber<OrderInfo>)this).SubscribeAsync();
    }

    /// <summary>
    /// Ожидание завершения обработки всех подписок
    /// </summary>
    public Task StrategyCompletion => Task.WhenAll(
        _marketDataFeed.Completion,
        _executionsFeed.Completion,
        _orderInfoFeed.Completion);

    /// <summary>
    /// Получение пользовательской сделки
    /// </summary>
    /// <param name="channelData">Пользовательская сделка</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns></returns>
    public abstract ValueTask HandleChannelDataAsync(UserExecution channelData, CancellationToken cancellationToken);

    /// <summary>
    /// Получение обновления пользовательской заявки
    /// </summary>
    /// <param name="channelData">Обновленное состояние пользовательской заявки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns></returns>
    public abstract ValueTask HandleChannelDataAsync(OrderInfo channelData, CancellationToken cancellationToken);

    /// <summary>
    /// Выставление заявки
    /// </summary>
    /// <param name="order"></param>
    protected virtual async ValueTask PlaceOrder(PlaceOrderRequest order)
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

            // Task.Run(async () =>
            // {
            //     var execution = new FastExecution(_channelsProvider, _timeProvider, new FastExecutionSettings
            //     {
            //         PriceLimit = order.Price,
            //         PriceSpread = 0.00,
            //         Side = order.Side,
            //         RequestedQuantity = order.Quantity,
            //     }, Logger);
            //     await execution.ExecutionCompletion;
            // });

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
            Logger.Error(exception, "Error while placing order");
            throw;
        }
    }

    protected virtual async ValueTask CancelOrder(CancelOrderRequest order)
    {
        try
        {
            Logger.Verbose("Cancelling order {ClientOrderId}", order.ClientOrderId);
            await _ordersCancellationFeed.WriteAsync(order);
            Logger.Debug("Order {ClientOrderId} cancelled", order.ClientOrderId);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Error while cancelling order");
            throw;
        }
    }

    public virtual Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.WhenAll(
            _marketDataFeedTask,
            _executionsFeedTask,
            _orderInfoFeedTask);
    }

    /// <summary>
    /// Ожидание завершения всех операций перед закрытием
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Logger.Debug("Disposing");
        await Task.WhenAll(
            Task.Run(async () =>
            {
                _ = await _ordersFeed.WaitToWriteAsync() && _ordersFeed.TryComplete();
                Logger.Debug("Orders feed closed");
            }),
            Task.Run(async () =>
            {
                _ = await _ordersCancellationFeed.WaitToWriteAsync() && _ordersCancellationFeed.TryComplete();
                Logger.Debug("Cancellation feed closed");
            })
        );
        await StrategyCompletion;
        Logger.Debug("Disposed");
    }

    /// <summary>
    /// Логгер
    /// </summary>
    public ILogger Logger { get; }

    public abstract ValueTask HandleChannelDataAsync(MarketDataItem channelData, CancellationToken cancellationToken);

    /// <inheritdoc />
    ChannelReader<OrderInfo> IChannelSubscriber<OrderInfo>.Reader => _orderInfoFeed;

    /// <inheritdoc />
    ChannelReader<MarketDataItem> IChannelSubscriber<MarketDataItem>.Reader => _marketDataFeed;

    /// <inheritdoc />
    ChannelReader<UserExecution> IChannelSubscriber<UserExecution>.Reader => _executionsFeed;
}
