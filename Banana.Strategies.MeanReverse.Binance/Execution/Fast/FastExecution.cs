using Banana.Backtest.Common.Models;
using Banana.Backtest.Emulator.ExchangeEmulator;
using Banana.Strategies.MeanReverse.Binance.DataFlow;
using Banana.Strategies.MeanReverse.Binance.Endpoints;
using Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Objects.Models.Futures;
using CryptoExchange.Net.Interfaces;
using Microsoft.Extensions.Options;

namespace Banana.Strategies.MeanReverse.Binance.Execution.Fast;

public class FastExecution : ExecutionBase<FastExecutionLaunchCommand>
{
    public FastExecution(DataFlowMediator mediator,
        IBinanceSocketClient binanceSocketClient,
        ICacheForSymbol<BinanceFuturesSymbol> instrumentsCache,
        ICacheForSymbol<ISymbolOrderBook> orderBooksCache,
        TimeProvider timeProvider,
        IOptions<ExecutionOptions> executionOptions,
        ILogger logger) : base(mediator, binanceSocketClient, instrumentsCache, orderBooksCache, timeProvider, executionOptions, logger)
    {
    }

    protected override async ValueTask Initialize(CancellationToken cancellationToken)
    {
        var takerPrice = (LaunchCommand.Side is Side.Long ? OrderBook!.BestAsk : OrderBook!.BestBid).Price;
        var quantity = AdjustQuantity(LaunchCommand.Quantity);
        var orderType = LaunchCommand.Type is FastExecutionType.BestOffer ? OrderType.Limit : OrderType.Market;
        await PlaceOrder(takerPrice, quantity, orderType, cancellationToken: cancellationToken);
    }
}
