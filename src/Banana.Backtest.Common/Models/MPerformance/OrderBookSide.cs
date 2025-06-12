namespace Banana.Backtest.Common.Models.MPerformance;

/// <summary>
/// One side of the order book.
/// </summary>
public class OrderBookSide
{
    private readonly SortedList<double, double> _levels;
    private readonly int _depth;
    private readonly int _direction;

    public Side Side { get; }

    public OrderBookSide(Side side, int depth = 50)
    {
        if (depth < 1 || depth > 500)
            throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be between 1 and 500.");
        Side = side;
        _direction = (int)side;
        if (_direction == 0)
            throw new ArgumentException("Side must be Long or Short.", nameof(side));

        // SortedList comparator: best-first ordering
        var comparer = Comparer<double>.Create((a, b) => _direction * b.CompareTo(a));
        _levels = new SortedList<double, double>(comparer);
        _depth = depth;
    }

    /// <summary>
    /// Replace or remove a price level.
    /// </summary>
    public void UpdateLevel(OrderBookLevel lvl)
    {
        if (lvl.Quantity <= 0)
            _levels.Remove(lvl.Price);
        else
            _levels[lvl.Price] = lvl.Quantity;
        if (_levels.Count > _depth)
            _levels.RemoveAt(_levels.Count - 1);
    }

    /// <summary>
    /// Batch update of levels.
    /// </summary>
    public void FillLevelUpdates(ReadOnlySpan<OrderBookLevel> updates, int length)
    {
        if (length < 0 || length > updates.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        for (int i = 0; i < length; i++)
            UpdateLevel(updates[i]);
    }

    /// <summary>
    /// Best price level.
    /// </summary>
    public OrderBookLevel BestOffer
        => _levels.Count > 0
            ? new OrderBookLevel(_levels.Keys[0], _levels.Values[0])
            : throw new InvalidOperationException("Empty book");

    /// <summary>
    /// All levels best-first.
    /// </summary>
    public OrderBookLevel[] GetSortedLevels()
    {
        int n = _levels.Count;
        var arr = new OrderBookLevel[n];
        for (int i = 0; i < n; i++)
            arr[i] = new OrderBookLevel(_levels.Keys[i], _levels.Values[i]);
        return arr;
    }

    /// <summary>
    /// Direct access to price->quantity map.
    /// </summary>
    public IReadOnlyDictionary<double, double> LevelMap => _levels;
}
