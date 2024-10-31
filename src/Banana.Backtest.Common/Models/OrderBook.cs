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

    public OrderBookSnapshot TakeSnapshot()
    {
        using var bidsEnumerator = Bids.GetEnumerator();
        using var asksEnumerator = Ask.GetEnumerator();

        var snapshot = new OrderBookSnapshot
        {
            Timestamp = _timestamp,
            Asks = new OrderBookLevels20(),
            Bids = new OrderBookLevels20()
        };

        var asksFinished = false;
        var bidsFinished = false;

        for (var i = 0; i < 20; i++)
        {
            if (!bidsFinished && bidsEnumerator.MoveNext())
            {
                var bid = bidsEnumerator.Current;
                snapshot.Bids[i] = bid.Value;
            }
            else
            {
                bidsFinished = true;
            }

            if (!asksFinished && asksEnumerator.MoveNext())
            {
                var ask = asksEnumerator.Current;
                snapshot.Asks[i] = ask.Value;
            }
            else
            {
                asksFinished = true;
            }
        }

        return snapshot;
    }
}

public struct OrderBookSnapshot
{
    public OrderBookLevels20 Bids;
    public OrderBookLevels20 Asks;
    public long Timestamp;
}

[System.Runtime.CompilerServices.InlineArray(20)]
public struct OrderBookLevels20
{
    private OrderBook.OrderBookLevel _bestValue;
}
