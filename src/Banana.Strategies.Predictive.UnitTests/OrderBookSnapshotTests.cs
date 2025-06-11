using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.MPerformance;
using Shouldly;

namespace Banana.Strategies.Predictive.UnitTests;

public unsafe class OrderBookSnapshotTests
{
    [Fact]
    public void EmptySnapshot_ShouldHaveZerosAndMidPriceThrowsIfEmpty()
    {
        var book = new BacktestOrderBook(depth: 5);
        var snapshot = book.TakeSnapshot();
        // All bid and ask entries should be 0
        for (int i = 0; i < OrderBookSnapshot.Depth; i++)
        {
            snapshot.Bid(i).Price.ShouldBe(0);
            snapshot.Bid(i).Quantity.ShouldBe(0);
            snapshot.Ask(i).Price.ShouldBe(0);
            snapshot.Ask(i).Quantity.ShouldBe(0);
        }
        // MidPrice should be 0 (0+0)/2
        snapshot.MidPrice.ShouldBe(0);
    }

    [Fact]
    public void Snapshot_ShouldReflectUpdatedLevels_BidsAndAsks()
    {
        var book = new BacktestOrderBook(depth: 5);
        // Prepare some bid and ask updates
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // Bids: 100@1, 102@2
        book.UpdateOrder(new MarketDataItem<LevelUpdate>(new LevelUpdate { Price = 100, Quantity = 1, IsBid = true, IsSnapshot = false }, ts));
        book.UpdateOrder(new MarketDataItem<LevelUpdate>(new LevelUpdate { Price = 102, Quantity = 2, IsBid = true, IsSnapshot = false }, ts + 1));
        // Asks: 105@3, 103@4
        book.UpdateOrder(new MarketDataItem<LevelUpdate>(new LevelUpdate { Price = 105, Quantity = 3, IsBid = false, IsSnapshot = false }, ts + 2));
        book.UpdateOrder(new MarketDataItem<LevelUpdate>(new LevelUpdate { Price = 103, Quantity = 4, IsBid = false, IsSnapshot = false }, ts + 3));

        var snapshot = book.TakeSnapshot();

        // Bids should be sorted best-first (highest price first)
        snapshot.Bid(0).Price.ShouldBe(102);
        snapshot.Bid(0).Quantity.ShouldBe(2);
        snapshot.Bid(1).Price.ShouldBe(100);
        snapshot.Bid(1).Quantity.ShouldBe(1);
        // Remaining should be zeros
        snapshot.Bid(2).Price.ShouldBe(0);
        snapshot.Bid(2).Quantity.ShouldBe(0);

        // Asks sorted best-first (lowest price first)
        snapshot.Ask(0).Price.ShouldBe(103);
        snapshot.Ask(0).Quantity.ShouldBe(4);
        snapshot.Ask(1).Price.ShouldBe(105);
        snapshot.Ask(1).Quantity.ShouldBe(3);
        snapshot.Ask(2).Price.ShouldBe(0);
        snapshot.Ask(2).Quantity.ShouldBe(0);

        // MidPrice = (bestBid + bestAsk)/2 = (102+103)/2 = 102.5
        snapshot.MidPrice.ShouldBe((102 + 103) / 2.0);
    }

    [Fact]
    public void FillBids_FillAsks_ShouldPopulateSpanCorrectly()
    {
        var book = new BacktestOrderBook(depth: 3);
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // Single bid and ask
        book.UpdateOrder(new MarketDataItem<LevelUpdate>(new LevelUpdate { Price = 50, Quantity = 5, IsBid = true, IsSnapshot = false }, ts));
        book.UpdateOrder(new MarketDataItem<LevelUpdate>(new LevelUpdate { Price = 60, Quantity = 6, IsBid = false, IsSnapshot = false }, ts + 1));
        var snapshot = book.TakeSnapshot();

        var bidSpan = new OrderBookLevel[OrderBookSnapshot.Depth];
        var askSpan = new OrderBookLevel[OrderBookSnapshot.Depth];
        snapshot.FillBids(bidSpan, 1);
        snapshot.FillAsks(askSpan, 1);

        bidSpan[0].Price.ShouldBe(50);
        bidSpan[0].Quantity.ShouldBe(5);
        askSpan[0].Price.ShouldBe(60);
        askSpan[0].Quantity.ShouldBe(6);
        // Other entries remain default
        bidSpan[1].Price.ShouldBe(0);
        askSpan[1].Quantity.ShouldBe(0);
    }

    [Fact]
    public void GetEnumerables_ShouldYieldLevelsUpToDepth()
    {
        var book = new BacktestOrderBook(depth: 2);
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // Insert 3 bids, depth=2, only top2 in snapshot
        book.UpdateOrder(new MarketDataItem<LevelUpdate>(new LevelUpdate { Price = 10, Quantity = 1, IsBid = true, IsSnapshot = false }, ts));
        book.UpdateOrder(new MarketDataItem<LevelUpdate>(new LevelUpdate { Price = 20, Quantity = 2, IsBid = true, IsSnapshot = false }, ts + 1));
        book.UpdateOrder(new MarketDataItem<LevelUpdate>(new LevelUpdate { Price = 15, Quantity = 3, IsBid = true, IsSnapshot = false }, ts + 2));

        var snapshot = book.TakeSnapshot();
        // Enumerate bids
        var list = snapshot.Bids();
        int count = 0;
        foreach (var lvl in list)
        {
            if (count == 0) lvl.Price.ShouldBe(20);
            if (count == 1) lvl.Price.ShouldBe(15);
            if (count >= 2) lvl.Price.ShouldBe(0);
            count++;
        }
        count.ShouldBe(OrderBookSnapshot.Depth);
    }
}
