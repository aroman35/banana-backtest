using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models.MarketData;

namespace Banana.Backtest.Common.Models;

public class OrderBook
{
    // Represents a single price level in the order book using struct
    public struct OrderBookLevel(double price, double quantity)
    {
        public double Price { get; } = price;
        public double Quantity { get; } = quantity;

        public OrderBookLevel UpdateQuantity(double newQuantity)
        {
            return new OrderBookLevel(Price, newQuantity);
        }
    }

    // Lists to store the levels for bids and asks
    private readonly SortedDictionary<double, OrderBookLevel> _bids = new(Comparer<double>.Create((x, y) => y.CompareTo(x)));
    private readonly SortedDictionary<double, OrderBookLevel> _asks = new();
    private long _timestamp;

    public OrderBookLevel BestBid => _bids.FirstOrDefault().Value;
    public OrderBookLevel BestAsk => _asks.FirstOrDefault().Value;
    public bool IsReady => _bids.Count > 0 && _asks.Count > 0;
    public bool IsConsistent => BestBid.Price.IsLower(BestAsk.Price);
    public DateTime Timestamp => _timestamp.AsDateTime();
    public SortedDictionary<double, OrderBookLevel> Bids => _bids;
    public SortedDictionary<double, OrderBookLevel> Ask => _asks;

    // Method to handle an update to the order book
    public void UpdateOrder(MarketDataItem<LevelUpdate> levelUpdate)
    {
        if (levelUpdate.Item.Quantity == 0)
        {
            // Remove the level if quantity is zero (means no orders left at that price)
            RemoveOrder(levelUpdate.Item.IsBid, levelUpdate.Item.Price);
        }
        else
        {
            // Add or update the level
            AddOrUpdateOrder(levelUpdate.Item.IsBid, levelUpdate.Item.Price, levelUpdate.Item.Quantity);
        }

        _timestamp = levelUpdate.Timestamp;
    }

    private void AddOrUpdateOrder(bool isBid, double price, double quantity)
    {
        var bookSide = isBid ? _bids : _asks;

        if (bookSide.ContainsKey(price))
        {
            // Update existing level by replacing with a new struct with updated quantity
            bookSide[price] = bookSide[price].UpdateQuantity(quantity);
        }
        else
        {
            // Add a new level
            bookSide[price] = new OrderBookLevel(price, quantity);
        }
    }

    private void RemoveOrder(bool isBid, double price)
    {
        var bookSide = isBid ? _bids : _asks;
        bookSide.Remove(price);
    }

    public override string ToString()
    {
        return $"[{Timestamp.ToLocalTime():O}]Bid: {BestBid.Price}x{BestBid.Quantity}, Ask: {BestAsk.Price}x{BestAsk.Quantity}";
    }

    public unsafe OrderBookSnapshot TakeSnapshot()
    {
        using var bidsEnumerator = Bids.GetEnumerator();
        using var asksEnumerator = Ask.GetEnumerator();

        var snapshot = new OrderBookSnapshot
        {
            Timestamp = _timestamp,
        };

        var asksFinished = false;
        var bidsFinished = false;

        for (var i = 0; i < OrderBookSnapshot.Depth; i++)
        {
            if (!bidsFinished && bidsEnumerator.MoveNext())
            {
                var bid = bidsEnumerator.Current;
                snapshot.BidPrices[i] = bid.Value.Price;
                snapshot.BidQuantities[i] = bid.Value.Quantity;
            }
            else
            {
                bidsFinished = true;
            }

            if (!asksFinished && asksEnumerator.MoveNext())
            {
                var ask = asksEnumerator.Current;
                snapshot.AskPrices[i] = ask.Value.Price;
                snapshot.AskQuantities[i] = ask.Value.Quantity;
            }
            else
            {
                asksFinished = true;
            }
        }

        return snapshot;
    }
}

public unsafe struct OrderBookSnapshot
{
    public const int Depth = 64;

    public fixed double BidPrices[Depth];
    public fixed double BidQuantities[Depth];
    public fixed double AskPrices[Depth];
    public fixed double AskQuantities[Depth];

    public void FillBids(Span<OrderBook.OrderBookLevel> bids, int length)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, Depth);

        fixed (OrderBook.OrderBookLevel* bidsPtr = bids)
        {
            for (var i = 0; i < length; i++)
            {
                var price = BidPrices[i];
                var quantity = BidQuantities[i];
                bidsPtr[i] = new OrderBook.OrderBookLevel(price, quantity);
            }
        }
    }

    public void FillAsks(Span<OrderBook.OrderBookLevel> asks, int length)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, Depth);

        fixed (OrderBook.OrderBookLevel* asksPtr = asks)
        {
            for (var i = 0; i < length; i++)
            {
                var price = AskPrices[i];
                var quantity = AskQuantities[i];
                asksPtr[i] = new OrderBook.OrderBookLevel(price, quantity);
            }
        }
    }

    public IEnumerable<OrderBook.OrderBookLevel> Bids()
    {
        for (var i = 0; i < Depth; i++)
        {
            double quantity;
            double price;
            unsafe
            {
                price = BidPrices[i];
                quantity = BidQuantities[i];
            }
            yield return new OrderBook.OrderBookLevel(price, quantity);
        }
    }

    public IEnumerable<OrderBook.OrderBookLevel> Asks()
    {
        for (var i = 0; i < Depth; i++)
        {
            double quantity;
            double price;
            unsafe
            {
                price = AskPrices[i];
                quantity = AskQuantities[i];
            }
            yield return new OrderBook.OrderBookLevel(price, quantity);
        }
    }

    public long Timestamp;
    public double MidPrice => (BidPrices[0] + AskPrices[0]) / 2;
}
