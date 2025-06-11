using System.Threading.Channels;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;
using Banana.Backtest.Emulator.ExchangeEmulator;
using Banana.Backtest.Emulator.Services;
using Microsoft.Extensions.Options;
using Serilog;
using Shouldly;

namespace Banana.Strategies.Predictive.UnitTests;

public class BacktestMatcherTests
{
    private class TestTimeProvider : TimeProvider
    {
        private long _ts;

        public TestTimeProvider(long initialTimestamp)
        {
            _ts = initialTimestamp;
        }

        public override long GetTimestamp() => _ts;
        public void Advance(long delta) => _ts += delta;
    }

    private IChannelsProvider CreateChannels(out Channel<MarketDataItem<TradeUpdate>> tradeIn,
        out Channel<MarketDataItem<LevelUpdate>> levelIn,
        out Channel<PlaceOrderRequest> placeIn,
        out Channel<CancelOrderRequest> cancelIn,
        out Channel<OrderInfo> orderStatusesOut,
        out Channel<UserExecution> executionsOut,
        out Channel<MarketDataItem> marketDataOut)
    {
        tradeIn = Channel.CreateUnbounded<MarketDataItem<TradeUpdate>>();
        levelIn = Channel.CreateUnbounded<MarketDataItem<LevelUpdate>>();
        placeIn = Channel.CreateUnbounded<PlaceOrderRequest>();
        cancelIn = Channel.CreateUnbounded<CancelOrderRequest>();
        executionsOut = Channel.CreateUnbounded<UserExecution>();
        orderStatusesOut = Channel.CreateUnbounded<OrderInfo>();
        marketDataOut = Channel.CreateUnbounded<MarketDataItem>();

        return new InMemoryChannelsProvider
        {
            TradeSource = tradeIn,
            LevelSource = levelIn,
            UserOrders = placeIn,
            CancelOrders = cancelIn,
            OrderStatuses = orderStatusesOut,
            UserExecutions = executionsOut,
            MarketData = marketDataOut
        };
    }

    private class InMemoryChannelsProvider : IChannelsProvider
    {
        public Channel<MarketDataItem<TradeUpdate>> TradeSource;
        public Channel<MarketDataItem<LevelUpdate>> LevelSource;
        public Channel<PlaceOrderRequest> UserOrders;
        public Channel<CancelOrderRequest> CancelOrders;
        public Channel<OrderInfo> OrderStatuses;
        public Channel<UserExecution> UserExecutions;
        public Channel<MarketDataItem> MarketData;

        public Channel<MarketDataItem<TMarketData>> GetMarketDataSourceChannel<TMarketData>()
            where TMarketData : unmanaged
        {
            if (typeof(TMarketData) == typeof(TradeUpdate))
                return (Channel<MarketDataItem<TMarketData>>)(object)TradeSource;
            if (typeof(TMarketData) == typeof(LevelUpdate))
                return (Channel<MarketDataItem<TMarketData>>)(object)LevelSource;
            throw new NotSupportedException();
        }

        public Channel<MarketDataItem<TMarketData>> GetMarketDataGatewayChannel<TMarketData>()
            where TMarketData : unmanaged
            => throw new NotSupportedException();

        public Channel<UserExecution> UserExecutionChannel => UserExecutions;
        public Channel<PlaceOrderRequest> UserOrdersChannel => UserOrders;
        public Channel<CancelOrderRequest> CancelOrdersChannel => CancelOrders;
        public Channel<OrderInfo> OrderStatusesChannel => OrderStatuses;
        public Channel<long> TimestampsFeed => Channel.CreateUnbounded<long>();
        public Channel<MarketDataItem> MarketDataCommonProviderChannel => MarketData;
    }

    private BacktestMatcher CreateMatcher(IChannelsProvider provider, TestTimeProvider time)
    {
        var options = Options.Create(new MatcherSettings { PriceStep = 1.0 });
        var logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();
        return new BacktestMatcher(provider, time, options, logger);
    }

    [Fact]
    public async Task LimitOrder_NoMatch_ShouldBeNewStatusOnly()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var time = new TestTimeProvider(now);
        CreateChannels(out _, out _, out var placeIn, out _, out var statusOut, out var execOut, out _);
        var matcher =
            CreateMatcher(
                CreateChannels(out _, out var lvlIn, out placeIn, out var cancelIn, out statusOut, out execOut,
                    out var mdOut), time);

        // Seed book ask at price=200
        await lvlIn.Writer.WriteAsync(
            new MarketDataItem<LevelUpdate>(new LevelUpdate { Price = 200, Quantity = 5, IsBid = false }, now));

        // Act: place limit buy at 100 (no match)
        var clientId = Guid.NewGuid();
        var pl = new PlaceOrderRequest
            { Side = Side.Long, Price = 100, Quantity = 3, ClientOrderId = clientId, OrderType = OrderType.Limit };
        await matcher.HandleChannelDataAsync(pl, CancellationToken.None);

        // Assert: one status New, no executions
        statusOut.Reader.TryRead(out var st).ShouldBeTrue();
        st.ClientOrderId.ShouldBe(clientId);
        st.Status.HasFlag(OrderStatus.New).ShouldBeTrue();
        execOut.Reader.TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task LimitOrder_Match_ShouldGenerateExecutionAndFill()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var time = new TestTimeProvider(now);
        var provider = CreateChannels(out _, out var lvlIn, out var placeIn, out var cancelIn, out var statusOut,
            out var execOut, out var mdOut);
        var matcher = CreateMatcher(provider, time);

        // Seed book ask at price=100, quantity=5
        await lvlIn.Writer.WriteAsync(
            new MarketDataItem<LevelUpdate>(new LevelUpdate { Price = 100, Quantity = 5, IsBid = false }, now));

        // Act: place limit buy at price=100 qty=3
        var clientId = Guid.NewGuid();
        var pl = new PlaceOrderRequest
            { Side = Side.Long, Price = 100, Quantity = 3, ClientOrderId = clientId, OrderType = OrderType.Limit };
        await matcher.HandleChannelDataAsync(pl, CancellationToken.None);

        // Assert: we should get execution
        execOut.Reader.TryRead(out var exec).ShouldBeTrue();
        exec.ClientOrderId.ShouldBe(clientId);
        exec.ExecutedQuantity.ShouldBe(3);
        exec.ExecutionPrice.ShouldBe(100);
        // And final status Fill
        statusOut.Reader.TryPeek(out var lastStatus).ShouldBeTrue();
        lastStatus.ClientOrderId.ShouldBe(clientId);
        lastStatus.Status.HasFlag(OrderStatus.Fill).ShouldBeTrue();
    }

    [Fact]
    public async Task CancelOrder_ShouldCancelAndNoFurtherExecution()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var time = new TestTimeProvider(now);
        var provider = CreateChannels(out _, out var lvlIn, out var placeIn, out var cancelIn, out var statusOut,
            out var execOut, out var mdOut);
        var matcher = CreateMatcher(provider, time);

        // Seed book ask at price=100
        await lvlIn.Writer.WriteAsync(
            new MarketDataItem<LevelUpdate>(new LevelUpdate { Price = 100, Quantity = 5, IsBid = false }, now));

        // Place limit buy at price=100 qty=5
        var clientId = Guid.NewGuid();
        var pl = new PlaceOrderRequest
            { Side = Side.Long, Price = 100, Quantity = 5, ClientOrderId = clientId, OrderType = OrderType.Limit };
        await matcher.HandleChannelDataAsync(pl, CancellationToken.None);

        // Cancel immediately before fill
        var cancel = new CancelOrderRequest { ClientOrderId = clientId };
        await matcher.HandleChannelDataAsync(cancel, CancellationToken.None);

        // Assert: status Cancelled
        statusOut.Reader.TryPeek(out var st).ShouldBeTrue();
        st.ClientOrderId.ShouldBe(clientId);
        st.Status.HasFlag(OrderStatus.Cancelled).ShouldBeTrue();
    }
}
