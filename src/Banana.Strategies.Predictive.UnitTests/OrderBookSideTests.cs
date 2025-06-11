using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MPerformance;
using Shouldly;

namespace Banana.Strategies.Predictive.UnitTests;

public class OrderBookSideTests
{
    [Fact]
    public void UpdateLevel_ShouldInsertLevels_InCorrectOrder_LongSide()
    {
        var ob = new OrderBookSide(Side.Long, depth: 5);

        // Insert levels out of order
        ob.UpdateLevel(new OrderBookLevel(101, 10));
        ob.UpdateLevel(new OrderBookLevel(103, 5));
        ob.UpdateLevel(new OrderBookLevel(102, 7));

        var sorted = ob.GetSortedLevels();
        // Long side: best-first descending
        sorted.Length.ShouldBe(3);
        sorted[0].Price.ShouldBe(103);
        sorted[1].Price.ShouldBe(102);
        sorted[2].Price.ShouldBe(101);
    }

    [Fact]
    public void UpdateLevel_ShouldRemoveLevel_WhenQuantityZero()
    {
        var ob = new OrderBookSide(Side.Short, depth: 5);
        ob.UpdateLevel(new OrderBookLevel(10, 1));
        ob.UpdateLevel(new OrderBookLevel(12, 2));
        ob.GetSortedLevels().Length.ShouldBe(2);

        // Remove one level
        ob.UpdateLevel(new OrderBookLevel(12, 0));
        var levels = ob.GetSortedLevels();
        levels.Length.ShouldBe(1);
        levels[0].Price.ShouldBe(10);
    }

    [Fact]
    public void BestOffer_ShouldReturnBestFirst_OnBothSides()
    {
        var bids = new OrderBookSide(Side.Long, depth: 3);
        bids.FillLevelUpdates(new[] { new OrderBookLevel(100, 1), new OrderBookLevel(105, 2), new OrderBookLevel(102, 3) }, 3);
        bids.BestOffer.Price.ShouldBe(105);

        var asks = new OrderBookSide(Side.Short, depth: 3);
        asks.FillLevelUpdates(new[] { new OrderBookLevel(100, 1), new OrderBookLevel(95, 2), new OrderBookLevel(98, 3) }, 3);
        asks.BestOffer.Price.ShouldBe(95);
    }

    [Fact]
    public void FillLevelUpdates_ShouldRespectDepth_LongsAndShorts()
    {
        var ob = new OrderBookSide(Side.Long, depth: 3);
        // Insert 4 levels, but only top 3 should remain
        var updates = new[]
        {
            new OrderBookLevel(100,1),
            new OrderBookLevel(101,1),
            new OrderBookLevel(102,1),
            new OrderBookLevel(103,1)
        };
        ob.FillLevelUpdates(updates, updates.Length);
        var levels = ob.GetSortedLevels();
        levels.Length.ShouldBe(3);
        levels.ShouldContain(l => l.Price == 103);
        levels.ShouldContain(l => l.Price == 102);
        levels.ShouldContain(l => l.Price == 101);
        levels.ShouldNotContain(l => l.Price == 100);
    }
}
