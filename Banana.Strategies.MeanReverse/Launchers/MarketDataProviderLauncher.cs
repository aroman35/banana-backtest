using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.Abstractions;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;
using ILogger = Serilog.ILogger;
using OrderBook = Tinkoff.InvestApi.V1.OrderBook;

namespace Banana.Strategies.MeanReverse.Launchers;

public class MarketDataProviderLauncher(
    IChannelsProvider channelsProvider,
    InvestApiClient investApiClient,
    ILogger logger,
    IOptions<StrategySettings> settings) : BackgroundService
{
    private readonly ChannelWriter<MarketDataItem> _marketDataGatewayChannel = channelsProvider.MarketDataCommonProviderChannel.Writer;
    private readonly ILogger _logger = logger.ForContext<MarketDataProviderLauncher>();
    private AsyncDuplexStreamingCall<MarketDataRequest, MarketDataResponse> _marketDataRequestChannel;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var futureResponse = await investApiClient.Instruments.FutureByAsync(new InstrumentRequest
        {
            IdType = InstrumentIdType.Ticker,
            Id = settings.Value.Ticker,
            ClassCode = "SPBFUT"
        });
        _marketDataRequestChannel = investApiClient.MarketDataStream.MarketDataStream();
        await _marketDataRequestChannel.RequestStream.WriteAsync(new MarketDataRequest
        {
            SubscribeOrderBookRequest = new SubscribeOrderBookRequest
            {
                Instruments = { new OrderBookInstrument { InstrumentId = futureResponse.Instrument.Uid, Depth = 50} },
                SubscriptionAction = SubscriptionAction.Subscribe
            }
        }, cancellationToken);
        await _marketDataRequestChannel.RequestStream.WriteAsync(new MarketDataRequest
        {
            SubscribeTradesRequest = new SubscribeTradesRequest
            {
                Instruments = { new TradeInstrument { InstrumentId = futureResponse.Instrument.Uid } },
                SubscriptionAction = SubscriptionAction.Subscribe,
                TradeType = TradeSourceType.TradeSourceExchange
            }
        }, cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var marketDataResponse in _marketDataRequestChannel.ResponseStream.ReadAllAsync(cancellationToken: stoppingToken))
        {
            switch (marketDataResponse.PayloadCase)
            {
                case MarketDataResponse.PayloadOneofCase.Orderbook:
                    await _marketDataGatewayChannel.WriteAsync(FromOrderBook(marketDataResponse.Orderbook), stoppingToken);
                    _logger.Verbose("Order book received");
                    break;
                case MarketDataResponse.PayloadOneofCase.Trade:
                    await _marketDataGatewayChannel.WriteAsync(FromTrade(marketDataResponse.Trade), stoppingToken);
                    _logger.Verbose("Trade received");
                    break;
                case MarketDataResponse.PayloadOneofCase.SubscribeTradesResponse:
                    foreach (var tradeSubscription in marketDataResponse.SubscribeTradesResponse.TradeSubscriptions)
                    {
                        _logger.Information("Subscribe Trades: {SubscriptionStatus}", tradeSubscription.SubscriptionStatus);
                    }
                    break;
                case MarketDataResponse.PayloadOneofCase.SubscribeOrderBookResponse:
                    foreach (var orderBookSubscription in marketDataResponse.SubscribeOrderBookResponse.OrderBookSubscriptions)
                    {
                        _logger.Information("Subscribe OrderBook: {SubscriptionStatus}", orderBookSubscription.SubscriptionStatus);
                    }
                    break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe MarketDataItem FromOrderBook(OrderBook orderBook)
    {
        var snapshot = new OrderBookSnapshot
        {
            Timestamp = orderBook.Time.ToDateTime().ToUnixTimeMilliseconds()
        };
        using var bidsEnumerator = orderBook.Bids.GetEnumerator();
        using var asksEnumerator = orderBook.Asks.GetEnumerator();
        var asksFinished = false;
        var bidsFinished = false;

        for (var i = 0; i < OrderBookSnapshot.Depth; i++)
        {
            if (!bidsFinished && bidsEnumerator.MoveNext())
            {
                var bid = bidsEnumerator.Current;
                if (bid is not null)
                {
                    snapshot.BidPrices[i] = decimal.ToDouble(bid.Price);
                    snapshot.BidQuantities[i] = bid.Quantity;
                }
            }
            else
            {
                bidsFinished = true;
            }

            if (!asksFinished && asksEnumerator.MoveNext())
            {
                var ask = asksEnumerator.Current;
                if (ask is not null)
                {
                    snapshot.AskPrices[i] = decimal.ToDouble(ask.Price);
                    snapshot.AskQuantities[i] = ask.Quantity;
                }
            }
            else
            {
                asksFinished = true;
            }
        }
        return MarketDataItem.FromOrderBook(snapshot);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MarketDataItem FromTrade(Trade trade)
    {
        return MarketDataItem.FromTrade(new MarketDataItem<TradeUpdate>(new TradeUpdate
        {
            Side = trade.Direction is TradeDirection.Buy ? Side.Long : Side.Short,
            Price = decimal.ToDouble(trade.Price),
            Quantity = trade.Quantity,
            TradeId = 0
        }, trade.Time.ToDateTime().ToUnixTimeMilliseconds()));
    }
}
