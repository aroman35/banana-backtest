using Banana.Backtest.Common.Models;
using Banana.Backtest.Emulator.ExchangeEmulator;
using Banana.Strategies.MeanReverse.Binance.DataFlow;
using Banana.Strategies.MeanReverse.Binance.Endpoints;
using Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;
using Banana.Strategies.MeanReverse.Binance.MarketData;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Objects.Models.Futures;
using CryptoExchange.Net.Interfaces;
using Microsoft.Extensions.Options;

namespace Banana.Strategies.MeanReverse.Binance.Execution.FrontRun;

public class FrontRunExecution : ExecutionBase<FrontRunExecutionLaunchCommand>
{
    private ILogger _logger;
    private decimal _bestLevelPrice;

    public FrontRunExecution(
        DataFlowMediator mediator,
        IBinanceSocketClient binanceSocketClient,
        ICacheForSymbol<BinanceFuturesSymbol> instrumentsCache,
        ICacheForSymbol<ISymbolOrderBook> orderBooksCache,
        TimeProvider timeProvider,
        IOptions<ExecutionOptions> executionOptions,
        ILogger logger) : base(mediator, binanceSocketClient, instrumentsCache, orderBooksCache, timeProvider, executionOptions, logger)
    {
        _logger = logger
            .ForContext(nameof(ExecutionId), ExecutionId)
            .ForContext<FrontRunExecution>();
    }

    protected override async ValueTask Initialize(CancellationToken cancellationToken)
    {
        _logger = _logger.ForContext(nameof(Symbol), Symbol);
        _bestLevelPrice = (LaunchCommand.Side is Side.Long ? OrderBook!.BestBid : OrderBook!.BestAsk).Price;
        var quantity = AdjustQuantity(LaunchCommand.Quantity / LaunchCommand.IcebergDivider);
        await PlaceOrder(_bestLevelPrice, quantity, OrderType.Limit, LaunchCommand.IsMakerOnly, cancellationToken);
    }

    protected override async ValueTask OnOrderBookTopUpdated(OrderBookTop orderBookTop, CancellationToken cancellationToken)
    {
        var bestLevelPriceUpdate = (LaunchCommand.Side is Side.Long ? orderBookTop.BestBid : orderBookTop.BestAsk).Price;
        var priceUpdated = _bestLevelPrice == bestLevelPriceUpdate;

        if (Result.RemainedQuantity <= 0)
            return;

        if (Result.NotConfirmedOrderIds.Count != 0)
            return;

        _bestLevelPrice = bestLevelPriceUpdate;
        if (priceUpdated)
        {
            foreach (var (clientOrderId, order) in Result.PendingOrders)
            {
                if ((order.Price - bestLevelPriceUpdate) * (int)LaunchCommand.Side < 0)
                {
                    var quantity = AdjustQuantity(order.Quantity);
                    if (order.Price == _bestLevelPrice)
                        continue;
                    await ReplaceOrder(quantity, clientOrderId, cancellationToken);
                }
            }
        }
        if (!Result.IsOrdersClosed())
            return;

        var minOrderQuantity = LaunchCommand.Quantity / LaunchCommand.IcebergDivider;
        await PlaceOrder(
            bestLevelPriceUpdate,
            AdjustQuantity(Math.Min(minOrderQuantity, Result.RemainedQuantity)),
            OrderType.Limit,
            LaunchCommand.IsMakerOnly,
            cancellationToken);
    }

    protected override HashSet<int> AllowedErrorCodes { get; } = [-5022, -5027, -2013];
}
