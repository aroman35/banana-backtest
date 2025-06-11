using Banana.Backtest.Common.Models.MarketData;

namespace Banana.Backtest.Common.Models.MPerformance;

/// <summary>
/// Full backtest order book.
/// </summary>
public class BacktestOrderBook
{
    private readonly OrderBookSide _bids;
    private readonly OrderBookSide _asks;

    /// <summary>Last update timestamp.</summary>
    public DateTime Timestamp { get; private set; }

    public BacktestOrderBook(int depth = 50)
    {
        _bids = new OrderBookSide(Side.Long, depth);
        _asks = new OrderBookSide(Side.Short, depth);
    }

    /// <summary>Delegate for current best bid.</summary>
    public Func<OrderBookLevel> BestBid => () => _bids.BestOffer;

    /// <summary>Delegate for current best ask.</summary>
    public Func<OrderBookLevel> BestAsk => () => _asks.BestOffer;

    /// <summary>Map of bid levels (price->quantity).</summary>
    public IReadOnlyDictionary<double, double> Bids => _bids.LevelMap;

    /// <summary>Map of ask levels (price->quantity).</summary>
    public IReadOnlyDictionary<double, double> Asks => _asks.LevelMap;

    /// <summary>Best offer by side.</summary>
    public OrderBookLevel BestOffer(Side side)
        => side == Side.Long ? _bids.BestOffer : _asks.BestOffer;

    /// <summary>Apply single update.</summary>
    public void UpdateOrder(MarketDataItem<LevelUpdate> md)
    {
        Timestamp = md.DateTime;
        var lu = md.Item;
        var lvl = new OrderBookLevel(lu.Price, lu.Quantity);
        if (lu.IsBid)
            _bids.UpdateLevel(lvl);
        else
            _asks.UpdateLevel(lvl);
    }

    /// <summary>Apply batch updates.</summary>
    public void UpdateOrders(ReadOnlySpan<MarketDataItem<LevelUpdate>> batch)
    {
        for (int i = 0; i < batch.Length; i++)
            UpdateOrder(batch[i]);
    }

    /// <summary>Take snapshot of book.</summary>
    public unsafe OrderBookSnapshot TakeSnapshot()
    {
        var snapshot = new OrderBookSnapshot();
        snapshot.Timestamp = Timestamp.Ticks;

        // Fill bids
        var bids = _bids.GetSortedLevels();
        int bidCount = Math.Min(bids.Length, OrderBookSnapshot.Depth);
        for (int i = 0; i < bidCount; i++)
        {
            snapshot.BidPrices[i] = bids[i].Price;
            snapshot.BidQuantities[i] = bids[i].Quantity;
        }

        for (int i = bidCount; i < OrderBookSnapshot.Depth; i++)
        {
            snapshot.BidPrices[i] = 0;
            snapshot.BidQuantities[i] = 0;
        }

        // Fill asks
        var asks = _asks.GetSortedLevels();
        int askCount = Math.Min(asks.Length, OrderBookSnapshot.Depth);
        for (int i = 0; i < askCount; i++)
        {
            snapshot.AskPrices[i] = asks[i].Price;
            snapshot.AskQuantities[i] = asks[i].Quantity;
        }

        for (int i = askCount; i < OrderBookSnapshot.Depth; i++)
        {
            snapshot.AskPrices[i] = 0;
            snapshot.AskQuantities[i] = 0;
        }

        return snapshot;
    }
}
