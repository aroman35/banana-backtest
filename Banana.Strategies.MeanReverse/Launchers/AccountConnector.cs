using System.Threading.Channels;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;
using CancelOrderRequest = Banana.Backtest.Emulator.Contracts.CancelOrderRequest;
using ILogger = Serilog.ILogger;

namespace Banana.Strategies.MeanReverse.Launchers;

public class AccountConnector :
    BackgroundService,
    IChannelSubscriber<PlaceOrderRequest>,
    IChannelSubscriber<CancelOrderRequest>
{
    private readonly ChannelWriter<UserExecution> _executionsGatewayChannel;
    private readonly ChannelWriter<OrderInfo> _ordersGatewayChannel;
    private readonly ChannelReader<PlaceOrderRequest> _userOrdersFeed;
    private readonly ChannelReader<CancelOrderRequest> _userOrdersCancellationFeed;
    private readonly Task _userOrdersFeedTask;
    private readonly Task _userOrdersCancellationFeedTask;
    public ILogger Logger { get; }

    private AsyncServerStreamingCall<OrderStateStreamResponse> _orderStatusesStream;
    private AsyncServerStreamingCall<TradesStreamResponse> _tradesStream;
    private string _instrumentId;
    private readonly InvestApiClient _investApiClient;
    private readonly IOptions<StrategySettings> _settings;

    public AccountConnector(IChannelsProvider channelsProvider,
        InvestApiClient investApiClient,
        ILogger logger,
        IOptions<StrategySettings> settings)
    {
        Logger = logger.ForContext<AccountConnector>();
        _investApiClient = investApiClient;
        _settings = settings;
        _executionsGatewayChannel = channelsProvider.UserExecutionChannel.Writer;
        _ordersGatewayChannel = channelsProvider.OrderStatusesChannel.Writer;
        _userOrdersFeed = channelsProvider.UserOrdersChannel.Reader;
        _userOrdersCancellationFeed = channelsProvider.CancelOrdersChannel.Reader;
        _userOrdersFeedTask = ((IChannelSubscriber<PlaceOrderRequest>)this).SubscribeAsync();
        _userOrdersCancellationFeedTask = ((IChannelSubscriber<CancelOrderRequest>)this).SubscribeAsync();
    }

    public async ValueTask HandleChannelDataAsync(CancelOrderRequest channelData, CancellationToken cancellationToken)
    {
        try
        {
            var cancelOrderResponse = await _investApiClient.Orders.CancelOrderAsync(
                new Tinkoff.InvestApi.V1.CancelOrderRequest
                {
                    AccountId = _settings.Value.AccountId,
                    OrderId = channelData.ClientOrderId.ToString("D")
                });
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Failed to cancel order");
        }
    }

    public async ValueTask HandleChannelDataAsync(PlaceOrderRequest channelData, CancellationToken cancellationToken)
    {
        try
        {
            var postOrderResponse = await _investApiClient.Orders.PostOrderAsync(new PostOrderRequest
            {
                AccountId = _settings.Value.AccountId,
                Direction = channelData.Side is Side.Long ? OrderDirection.Buy : OrderDirection.Sell,
                InstrumentId = _instrumentId,
                OrderId = channelData.ClientOrderId.ToString("D"),
                OrderType = channelData.OrderType is Backtest.Emulator.ExchangeEmulator.OrderType.Limit
                    ? OrderType.Limit
                    : OrderType.Market,
                Price = (decimal)channelData.Price,
                Quantity = (long)channelData.Quantity,
                PriceType = PriceType.Point,
                TimeInForce = TimeInForceType.TimeInForceDay
            });
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Error in placing order");
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var futureResponse = await _investApiClient.Instruments.FutureByAsync(new InstrumentRequest
        {
            IdType = InstrumentIdType.Ticker,
            Id = _settings.Value.Ticker,
            ClassCode = "SPBFUT"
        });
        _instrumentId = futureResponse.Instrument.Uid;

        _orderStatusesStream = _investApiClient.OrdersStream.OrderStateStream(new OrderStateStreamRequest
        {
            Accounts = { _settings.Value.AccountId }
        }, cancellationToken: cancellationToken);
        _tradesStream = _investApiClient.OrdersStream.TradesStream(new TradesStreamRequest
        {
            Accounts = { _settings.Value.AccountId }
        }, cancellationToken: cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(
            _userOrdersFeed.Completion,
            _userOrdersCancellationFeed.Completion
        );
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.WhenAll(
            Task.Run(async () =>
            {
                await foreach (var orderStateStreamResponse in _orderStatusesStream.ResponseStream.ReadAllAsync(stoppingToken))
                {
                    switch (orderStateStreamResponse.PayloadCase)
                    {
                        case OrderStateStreamResponse.PayloadOneofCase.Subscription:
                            Logger.Information("Received Subscription: {Status}", orderStateStreamResponse.Subscription.Status);
                            break;
                        case OrderStateStreamResponse.PayloadOneofCase.OrderState:
                            if (orderStateStreamResponse.OrderState.InstrumentUid != _instrumentId)
                                break;
                            if (Guid.TryParse(orderStateStreamResponse.OrderState.OrderId, out _))
                                break;
                            var order = Convert(orderStateStreamResponse.OrderState);
                            await _ordersGatewayChannel.WriteAsync(order, stoppingToken);
                            Logger.Debug("Order update received: {@OrderUpdate}", order);
                            break;
                    }
                }
            }, stoppingToken),
            Task.Run(async () =>
            {
                await foreach (var tradesStreamResponse in _tradesStream.ResponseStream.ReadAllAsync(stoppingToken))
                {
                    switch (tradesStreamResponse.PayloadCase)
                    {
                        case TradesStreamResponse.PayloadOneofCase.Subscription:
                            Logger.Information("Received Subscription: {Status}", tradesStreamResponse.Subscription.Status);
                            break;
                        case TradesStreamResponse.PayloadOneofCase.OrderTrades:
                            if (tradesStreamResponse.OrderTrades.InstrumentUid != _instrumentId)
                                break;
                            foreach (var orderTrade in Convert(tradesStreamResponse.OrderTrades))
                            {
                                await _executionsGatewayChannel.WriteAsync(orderTrade, stoppingToken);
                                Logger.Debug("Execution received: {@Execution}", orderTrade);
                            }
                            break;
                    }
                }
            }, stoppingToken));
    }

    private static OrderInfo Convert(OrderStateStreamResponse.Types.OrderState orderState)
    {
        var executedQuantity = orderState.Trades.Sum(x => x.Quantity);
        return new OrderInfo
        {
            Id = long.Parse(orderState.OrderId),
            Type = orderState.OrderType is OrderType.Limit
                ? Backtest.Emulator.ExchangeEmulator.OrderType.Limit
                : Backtest.Emulator.ExchangeEmulator.OrderType.Market,
            Side = orderState.Direction is OrderDirection.Buy ? Side.Long : Side.Short,
            Price = decimal.ToDouble(orderState.OrderPrice),
            Timestamp = orderState.CreatedAt.ToDateTime().ToUnixTimeMilliseconds(),
            StatusUpdateTimestamp = orderState.CompletionTime.ToDateTime().ToUnixTimeMilliseconds(),
            ClientOrderId =
                orderState.HasOrderRequestId && Guid.TryParse(orderState.OrderRequestId, out var clientOrderId)
                    ? clientOrderId
                    : Guid.Empty,
            RequestedQuantity = orderState.LotsRequested,
            ExecutedQuantity = executedQuantity,
            RemainingQuantity = orderState.LotsRequested - executedQuantity,
            Status = orderState.ExecutionReportStatus switch
            {
                OrderExecutionReportStatus.ExecutionReportStatusUnspecified => OrderStatus.Unspecified,
                OrderExecutionReportStatus.ExecutionReportStatusFill => OrderStatus.Fill,
                OrderExecutionReportStatus.ExecutionReportStatusRejected => OrderStatus.Rejected,
                OrderExecutionReportStatus.ExecutionReportStatusCancelled => OrderStatus.Cancelled,
                OrderExecutionReportStatus.ExecutionReportStatusNew => OrderStatus.New,
                OrderExecutionReportStatus.ExecutionReportStatusPartiallyfill => OrderStatus.PartiallyFill,
                _ => throw new ArgumentOutOfRangeException()
            },
            RejectionReason = orderState.StatusInfo is OrderStateStreamResponse.Types.StatusCauseInfo.CauseUnspecified
                ? RejectionReason.None
                : RejectionReason.Exchange,
        };
    }

    private static IEnumerable<UserExecution> Convert(OrderTrades orderTrades)
    {
        foreach (var trade in orderTrades.Trades)
        {
            yield return new UserExecution
            {
                TradeId = long.Parse(trade.TradeId),
                OrderId = long.Parse(orderTrades.OrderId),
                Side = orderTrades.Direction is OrderDirection.Buy ? Side.Long : Side.Short,
                ExecutionPrice = decimal.ToDouble(trade.Price),
                ExecutedQuantity = trade.Quantity,
                Timestamp = trade.DateTime.ToDateTime().ToUnixTimeMilliseconds()
            };
        }
    }

    /// <inheritdoc />
    ChannelReader<CancelOrderRequest> IChannelSubscriber<CancelOrderRequest>.Reader => _userOrdersCancellationFeed;

    /// <inheritdoc />
    ChannelReader<PlaceOrderRequest> IChannelSubscriber<PlaceOrderRequest>.Reader => _userOrdersFeed;

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
