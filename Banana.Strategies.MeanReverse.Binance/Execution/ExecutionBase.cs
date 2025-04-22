using Banana.Backtest.Common.Models;
using Banana.Backtest.Emulator.ExchangeEmulator;
using Banana.Strategies.MeanReverse.Binance.DataFlow;
using Banana.Strategies.MeanReverse.Binance.Endpoints;
using Banana.Strategies.MeanReverse.Binance.Execution.Models;
using Banana.Strategies.MeanReverse.Binance.Extensions;
using Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;
using Banana.Strategies.MeanReverse.Binance.MarketData;
using Binance.Net.Enums;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Objects.Models.Futures;
using Binance.Net.Objects.Models.Futures.Socket;
using CryptoExchange.Net.Interfaces;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Options;
using Serilog.Events;
using OrderStatus = Banana.Backtest.Emulator.Contracts.OrderStatus;
using UserOrder = Banana.Strategies.MeanReverse.Binance.Execution.Models.UserOrder;

namespace Banana.Strategies.MeanReverse.Binance.Execution;

public abstract class ExecutionBase<TLaunchExecutionCommand> : IExecution<TLaunchExecutionCommand>, IAsyncDisposable
    where TLaunchExecutionCommand : LaunchExecutionCommandBase
{
    private readonly ICacheForSymbol<BinanceFuturesSymbol> _instrumentsCache;
    private readonly ICacheForSymbol<ISymbolOrderBook> _orderBooksCache;
    private readonly DataFlowMediator _mediator;
    private readonly ManualResetEventSlim _onExchangeOperation = new(true);
    private readonly IBinanceSocketClient _binanceSocketClient;
    private readonly TimeProvider _clock;
    private readonly TaskCompletionSource<ExecutionResult> _executionCompletionTask = new();
    private readonly IOptions<ExecutionOptions> _executionOptions;

    private CancellationTokenSource _executionCancellationTokenSource = new();
    private ILogger _logger;

    private protected readonly Guid ExecutionId;
    private protected BinanceFuturesSymbol InstrumentInfo = null!;
    private protected ISymbolOrderBook OrderBook = null!;
    private protected ExecutionResult Result = null!;
    private protected TLaunchExecutionCommand LaunchCommand = null!;

    protected ExecutionBase(
        DataFlowMediator mediator,
        IBinanceSocketClient binanceSocketClient,
        ICacheForSymbol<BinanceFuturesSymbol> instrumentsCache,
        ICacheForSymbol<ISymbolOrderBook> orderBooksCache,
        TimeProvider timeProvider,
        IOptions<ExecutionOptions> executionOptions,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(mediator, nameof(mediator));
        ArgumentNullException.ThrowIfNull(binanceSocketClient, nameof(binanceSocketClient));
        ArgumentNullException.ThrowIfNull(timeProvider, nameof(timeProvider));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        ArgumentNullException.ThrowIfNull(executionOptions, nameof(executionOptions));
        ArgumentNullException.ThrowIfNull(instrumentsCache, nameof(instrumentsCache));
        ArgumentNullException.ThrowIfNull(orderBooksCache, nameof(orderBooksCache));

        _mediator = mediator;
        _binanceSocketClient = binanceSocketClient;
        _executionOptions = executionOptions;
        _clock = timeProvider;
        _instrumentsCache = instrumentsCache;
        _orderBooksCache = orderBooksCache;

        ExecutionId = Guid.NewGuid();
        _logger = logger
            .ForContext<ExecutionBase<TLaunchExecutionCommand>>()
            .ForContext(nameof(ExecutionId), ExecutionId);
    }

    public string Symbol { get; private set; } = null!;

    public async Task<ExecutionResult> ExecuteAsync(
        TLaunchExecutionCommand command,
        CancellationToken cancellationToken)
    {
        Symbol = command.Symbol;
        _logger = _logger.ForContext(nameof(Symbol), Symbol);
        LaunchCommand = command;
        var orderBook = _orderBooksCache.Get(Symbol);
        var instrumentInfo = _instrumentsCache.Get(Symbol);

        OrderBook = orderBook;
        InstrumentInfo = instrumentInfo;

        Result = ExecutionResult.Created(command, _clock.GetUtcNow().DateTime, ExecutionId);
        _logger.Debug("Created");

        cancellationToken.Register(Cancel);
        _executionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (!await IsValidationPassed())
            return await _executionCompletionTask.Task;

        _executionCancellationTokenSource.CancelAfter(command.Timeout);

        if (OrderBook.Status is not OrderBookStatus.Synced)
        {
            await OrderBook.StartAsync();
        }

        Result.Launched();

        await Initialize(_executionCancellationTokenSource.Token);
        if (_executionCompletionTask.Task.Status is TaskStatus.RanToCompletion)
            return await _executionCompletionTask.Task;

        var orderBookReader = _mediator.StreamForSymbol<OrderBookTop>(Symbol, _executionCancellationTokenSource.Token);
        var userTradesReaderReader = _mediator.StreamForSymbol<BinanceFuturesStreamTradeUpdate>(Symbol, _executionCancellationTokenSource.Token);
        var userOrdersReaderReader = _mediator.StreamForSymbol<BinanceFuturesStreamOrderUpdate>(Symbol, _executionCancellationTokenSource.Token);

        await Task.WhenAll(
            HandleStreamData(
                orderBookReader,
                OnOrderBookUpdateReceived,
                SetError,
                _executionCancellationTokenSource.Token),
            HandleStreamData(
                userTradesReaderReader,
                OnUserTradeReceived,
                SetError,
                _executionCancellationTokenSource.Token),
            HandleStreamData(
                userOrdersReaderReader,
                OnUserOrderUpdated,
                SetError,
                _executionCancellationTokenSource.Token)
        );

        return await _executionCompletionTask.Task;
    }

    public void Cancel()
    {
        if (Result.Status.IsCompleted())
            return;
        if (!_executionCancellationTokenSource.IsCancellationRequested)
            _executionCancellationTokenSource.Cancel(false);
        Result.FinishCanceled(_clock.GetUtcNow().DateTime);
        _executionCompletionTask.TrySetResult(Result);
        _logger.Information("Execution canceled");
    }

    // private async Task HandleStreamData<TData>(
    //     IAsyncEnumerable<TData> dataSource,
    //     Func<TData, CancellationToken, ValueTask> onDataReceived,
    //     Func<Exception, Task> onError,
    //     CancellationToken cancellationToken)
    // {
    //     ArgumentNullException.ThrowIfNull(dataSource, nameof(dataSource));
    //     ArgumentNullException.ThrowIfNull(onDataReceived, nameof(onDataReceived));
    //     ArgumentNullException.ThrowIfNull(onError, nameof(onError));
    //
    //     await Task.Factory.StartNew(async void () =>
    //     {
    //         try
    //         {
    //             await Task.Yield();
    //             await foreach (var data in dataSource.WithCancellation(cancellationToken))
    //             {
    //                 if (data is not null)
    //                     await onDataReceived(data, cancellationToken);
    //             }
    //         }
    //         catch (OperationCanceledException)
    //         {
    //         }
    //         catch (Exception exception)
    //         {
    //             await onError(exception);
    //         }
    //     },
    //         cancellationToken,
    //         TaskCreationOptions.LongRunning,
    //         TaskScheduler.Default);
    // }

    private async Task PerformExchangeDelayedOperationAsync(
        Func<CancellationToken, Task> eventHandler,
        CancellationToken cancellationToken)
    {
        using (ExchangeDelayedOperation.Perform(_onExchangeOperation, cancellationToken))
        {
            try
            {
                await eventHandler(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                await SetError(exception);
            }
        }
    }

    protected Task SetError(Exception exception)
    {
        return SetError($"Failed with unhandled exception [{exception.GetType().Name}]: {exception.Message}");
    }

    protected async Task SetError(string? message)
    {
        _logger.Error("Failed : {Error}", message);
        Result.FinishFailure(_clock.GetUtcNow().DateTime, message ?? "Unknown error");
        _executionCompletionTask.TrySetResult(Result);
        if (!_executionCancellationTokenSource.IsCancellationRequested)
            await _executionCancellationTokenSource.CancelAsync();
    }

    private async ValueTask OnOrderBookUpdateReceived(OrderBookTop orderBookTop, CancellationToken cancellationToken)
    {
        if (!_onExchangeOperation.IsSet)
            return;
        await OnOrderBookTopUpdated(orderBookTop, cancellationToken);
    }

    private async ValueTask OnUserTradeReceived(BinanceFuturesStreamTradeUpdate userTrade,
        CancellationToken cancellationToken)
    {
        var trade = UserTrade.FromBinance(userTrade);
        if (!Result.TradeReceived(trade))
        {
            _logger
                .ForContext("ClientOrderId", trade.ClientOrderId)
                .Debug(
                    "UNKNOWN Trade received: {Side} {Price}x{Quantity} (TradeId:{TradeId}|OrderId:{OrderId})",
                    trade.Side,
                    trade.Price,
                    trade.Quantity,
                    trade.Id,
                    trade.OrderId);
            return;
        }
        if (_logger.IsEnabled(LogEventLevel.Debug))
        {
            _logger
                .ForContext("ClientOrderId", trade.ClientOrderId)
                .Debug(
                    "Trade received: {Side} {Price}x{Quantity} (TradeId:{TradeId}|OrderId:{OrderId})",
                    trade.Side,
                    trade.Price,
                    trade.Quantity,
                    trade.Id,
                    trade.OrderId);
        }

        await OnUserTradeReceived(trade, cancellationToken);
        if (Result.IsExecutionComplete())
        {
            Finish();
        }

        await _mediator.Send(Result);
    }

    private async ValueTask OnUserOrderUpdated(BinanceFuturesStreamOrderUpdate userOrder,
        CancellationToken cancellationToken)
    {
        var order = UserOrder.FromBinance(userOrder.UpdateData);
        if (!Result.OrderUpdateReceived(order))
            return;

        if (_logger.IsEnabled(LogEventLevel.Debug))
        {
            _logger
                .ForContext("ClientOrderId", order.ClientOrderId)
                .Debug(
                    "Order update [{OrderStatus}] {Side} {Price}x{Quantity} (OrderId:{OrderId})",
                    order.Status,
                    order.Side,
                    order.Price,
                    order.Quantity,
                    order.Id);
        }

        await OnUserOrderReceived(order, cancellationToken);
        if (Result.IsExecutionComplete())
        {
            Finish();
        }
        await _mediator.Send(Result);
    }

    private async ValueTask<bool> IsValidationPassed()
    {
        if (!LaunchCommand.Validate(out var error))
        {
            await SetError(error);
            return false;
        }

        if (InstrumentInfo.MinNotionalFilter is null ||
            InstrumentInfo.LotSizeFilter is null ||
            InstrumentInfo.PriceFilter is null)
        {
            await SetError("Instrument info is missing or filters are unspecified");
            return false;
        }

        var takerPrice = (LaunchCommand.Side is Side.Long ? OrderBook.BestAsk : OrderBook.BestBid).Price;

        if (LaunchCommand.Quantity < InstrumentInfo.MinOrderQuantity(takerPrice))
        {
            await SetError("Requested quantity must be greater than minimum notional");
            return false;
        }

        return true;
    }

    private async ValueTask<bool> IsPreTradeControlPassed(decimal price, decimal quantity, OrderType orderType)
    {
        var lotSizeFilter = InstrumentInfo.LotSizeFilter;
        var minNotionalFilter = InstrumentInfo.MinNotionalFilter;

        if (orderType is OrderType.Limit)
        {
            if (price * quantity < minNotionalFilter!.MinNotional)
            {
                await SetError("Requested quantity must be greater than minimum notional");
                return false;
            }

            if (price % InstrumentInfo.PriceFilter!.TickSize != 0)
            {
                await SetError("Price requested doesn't meet price-step requirements");
                return false;
            }

            if (price < InstrumentInfo.PriceFilter.MinPrice || price > InstrumentInfo.PriceFilter.MaxPrice)
            {
                await SetError("Price requested doesn't meet min-max price requirements");
                return false;
            }
        }

        if (quantity % lotSizeFilter!.StepSize != 0)
        {
            await SetError("Quantity requested doesn't meet quantity-step requirements");
            return false;
        }

        return true;
    }

    protected virtual ValueTask Initialize(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    protected virtual ValueTask OnOrderBookTopUpdated(OrderBookTop orderBookTop, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    protected virtual ValueTask OnUserOrderReceived(UserOrder userOrder, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    protected virtual ValueTask OnUserTradeReceived(UserTrade userTrade, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    protected virtual HashSet<int> AllowedErrorCodes => [];

    protected async Task ReplaceOrder(
        decimal quantityUpdate,
        Guid clientOrderId,
        CancellationToken cancellationToken = default)
    {
        await PerformExchangeDelayedOperationAsync(ReplaceOrderInnerTask, cancellationToken);
        return;

        async Task ReplaceOrderInnerTask(CancellationToken ctx)
        {
            if (_logger.IsEnabled(LogEventLevel.Debug))
            {
                _logger
                    .ForContext("ClientOrderId", clientOrderId)
                    .Debug(
                        "Replacing {Side} order {Quantity} for the best match",
                        LaunchCommand.Side,
                        quantityUpdate);
            }

            var replaceOrderResponse = await _binanceSocketClient.UsdFuturesApi.Trading.EditOrderAsync(
                Symbol,
                LaunchCommand.Side is Side.Long ? OrderSide.Buy : OrderSide.Sell,
                quantityUpdate,
                price: null,
                priceMatch: PriceMatch.Queue,
                origClientOrderId: clientOrderId.ToString(),
                ct: ctx);

            if (replaceOrderResponse.Success)
            {
                if (_logger.IsEnabled(LogEventLevel.Debug))
                {
                    _logger
                        .ForContext("ClientOrderId", clientOrderId)
                        .Debug(
                            "{Side} order {Price}x{Quantity} for the best match successfully replaced",
                            LaunchCommand.Side,
                            replaceOrderResponse.Data.Result.Price,
                            quantityUpdate);
                }

                return;
            }

            _logger
                .ForContext("ClientOrderId", clientOrderId)
                .Warning(
                    "Order {Side} {Quantity} failed to replace ({Code}): {Error}",
                    LaunchCommand.Side,
                    quantityUpdate,
                    replaceOrderResponse.Error?.Code,
                    replaceOrderResponse.Error?.Message);

            if (replaceOrderResponse.Error?.Code is null)
                return;

            if (AllowedErrorCodes.Contains(replaceOrderResponse.Error.Code.Value))
            {
                return;
            }

            await SetError(replaceOrderResponse.Error?.Message);
        }
    }

    protected async Task PlaceOrder(decimal price, decimal quantity, OrderType orderType, bool forceMaker = false,
        CancellationToken cancellationToken = default)
    {
        await PerformExchangeDelayedOperationAsync(PlaceOrderInnerTask, cancellationToken);
        return;

        async Task PlaceOrderInnerTask(CancellationToken ctx)
        {
            if (!await IsPreTradeControlPassed(price, quantity, orderType))
                return;

            var clientOrderId = Guid.NewGuid();
            var isMarket = orderType is OrderType.Market;
            Result.OrderPlaced(clientOrderId);
            if (_logger.IsEnabled(LogEventLevel.Debug))
            {
                _logger
                    .ForContext("ClientOrderId", clientOrderId)
                    .Debug("Placing {Side} order {Price}x{Quantity}", LaunchCommand.Side, price, quantity);
            }

            TimeInForce? timeInForce = _executionOptions.Value.LimitOrderDefaultTimeInForce;
            if (isMarket)
                timeInForce = null;
            if (forceMaker)
                timeInForce = TimeInForce.GoodTillCrossing;

            DateTime? goodTillDateValue = timeInForce is TimeInForce.GoodTillDate
                ? _clock.GetUtcNow().Add(_executionOptions.Value.OrderDefaultTimeout).UtcDateTime
                : null;

            var orderResponse = await _binanceSocketClient.UsdFuturesApi.Trading.PlaceOrderAsync(
                Symbol,
                LaunchCommand.Side is Side.Long ? OrderSide.Buy : OrderSide.Sell,
                isMarket ? FuturesOrderType.Market : FuturesOrderType.Limit,
                quantity, isMarket ? null : forceMaker ? null : price,
                priceMatch: forceMaker ? PriceMatch.Queue : null,
                newClientOrderId: clientOrderId.ToString(),
                timeInForce: timeInForce,
                goodTillDate: goodTillDateValue,
                ct: ctx);

            if (orderResponse.Success)
            {
                if (_logger.IsEnabled(LogEventLevel.Debug))
                {
                    _logger
                        .ForContext("ClientOrderId", clientOrderId)
                        .Debug("Order {Side} {Price}x{Quantity} placed", LaunchCommand.Side, price, quantity);
                }

                return;
            }

            _logger
                .ForContext("ClientOrderId", clientOrderId)
                .Warning(
                    "Order {Side} {Price}x{Quantity} failed to place ({Code}): {Error}",
                    LaunchCommand.Side,
                    price,
                    quantity,
                    orderResponse.Error?.Code,
                    orderResponse.Error?.Message);

            if (orderResponse.Error?.Code is not null && AllowedErrorCodes.Contains(orderResponse.Error.Code.Value))
            {
                Result.OrderSafeRejected(clientOrderId);
                return;
            }

            await SetError(orderResponse.Error?.Message);
        }
    }

    protected async Task CancelOrder(Guid clientOrderId, CancellationToken cancellationToken = default)
    {
        await PerformExchangeDelayedOperationAsync(CancelOrderInnerTask, cancellationToken);
        return;

        async Task CancelOrderInnerTask(CancellationToken ctx)
        {
            var cancelOrderResult = await _binanceSocketClient.UsdFuturesApi.Trading.CancelOrderAsync(
                Symbol,
                origClientOrderId: clientOrderId.ToString(),
                ct: ctx);

            if (cancelOrderResult.Error is not null)
            {
                _logger
                    .ForContext("ClientOrderId", clientOrderId)
                    .Warning(
                        "Failed to cancel order ({Code}): {Error})",
                        cancelOrderResult.Error.Code,
                        cancelOrderResult.Error.Message);
            }
        }
    }

    protected async Task CancelAllOrders(CancellationToken cancellationToken = default)
    {
        foreach (var clientOrderId in Result.NotConfirmedOrderIds)
        {
            await CancelOrder(clientOrderId, cancellationToken);
        }

        foreach (var (clientOrderId, placedOrder) in Result.PlacedOrders)
        {
            if (placedOrder.Status is OrderStatus.New or OrderStatus.PartiallyFill)
                await CancelOrder(clientOrderId, cancellationToken);
        }
    }

    protected void Finish()
    {
        if (!_executionCancellationTokenSource.IsCancellationRequested)
            _executionCancellationTokenSource.Cancel(false);

        Result.FinishSuccess(_clock.GetUtcNow().DateTime);
        _executionCompletionTask.TrySetResult(Result);
        _logger.Information("Execution finished");
    }

    protected decimal AdjustQuantity(decimal quantity)
    {
        var stepDelta = quantity % InstrumentInfo.LotSizeFilter!.StepSize;
        if (stepDelta == 0)
            return quantity;

        var quantityAddition = LaunchCommand.QuantitySpread switch
        {
            QuantitySpread.Upper => InstrumentInfo.LotSizeFilter!.StepSize - stepDelta,
            QuantitySpread.Lower => -stepDelta,
            _ => 0
        };
        quantity += quantityAddition;
        return quantity;
    }

    public async ValueTask DisposeAsync()
    {
        await CancelAllOrders();
        _onExchangeOperation.Dispose();
        _executionCancellationTokenSource.Dispose();
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }
}
