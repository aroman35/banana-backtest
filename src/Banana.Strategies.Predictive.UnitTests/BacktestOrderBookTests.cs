using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.MPerformance;
using Shouldly;

namespace Banana.Strategies.Predictive.UnitTests;

public class BacktestOrderBookTests
{
    [Fact]
    public void UpdateOrder_ShouldUpdateBidsAndAsksAndTimestamp()
    {
        var book = new BacktestOrderBook(depth: 5);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var item1 = new MarketDataItem<LevelUpdate>(new LevelUpdate { Price = 50, Quantity = 5, IsBid = true, IsSnapshot = false }, now);
        book.UpdateOrder(item1);
        book.Timestamp.ShouldBe(item1.DateTime);
        book.BestOffer(Side.Long).Price.ShouldBe(50);

        var item2 = new MarketDataItem<LevelUpdate>(new LevelUpdate { Price = 60, Quantity = 2, IsBid = false, IsSnapshot = false }, now + 100);
        book.UpdateOrder(item2);
        book.Timestamp.ShouldBe(item2.DateTime);
        book.BestOffer(Side.Short).Price.ShouldBe(60);
    }

    [Fact]
    public void UpdateOrders_ShouldBatchUpdateCorrectly()
    {
        var book = new BacktestOrderBook(depth: 5);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var updates = new MarketDataItem<LevelUpdate>[]
        {
            new MarketDataItem<LevelUpdate>(new LevelUpdate{ Price=10, Quantity=1, IsBid=true, IsSnapshot=false }, now),
            new MarketDataItem<LevelUpdate>(new LevelUpdate{ Price=20, Quantity=2, IsBid=false, IsSnapshot=false }, now),
            new MarketDataItem<LevelUpdate>(new LevelUpdate{ Price=15, Quantity=3, IsBid=true, IsSnapshot=false }, now)
        };
        book.UpdateOrders(updates);
        book.BestOffer(Side.Long).Price.ShouldBe(15);
        book.BestOffer(Side.Short).Price.ShouldBe(20);
    }

    [Fact]
    public void UpdateOrder_ShouldHandleRemoval()
    {
        var book = new BacktestOrderBook(depth: 5);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var add = new MarketDataItem<LevelUpdate>(new LevelUpdate{ Price=30, Quantity=5, IsBid=true, IsSnapshot=false }, t);
        var remove = new MarketDataItem<LevelUpdate>(new LevelUpdate{ Price=30, Quantity=0, IsBid=true, IsSnapshot=false }, t+1);
        book.UpdateOrder(add);
        book.BestOffer(Side.Long).Price.ShouldBe(30);
        book.UpdateOrder(remove);
        Should.Throw<InvalidOperationException>(() => book.BestOffer(Side.Long));
    }
}
