using Banana.Backtest.Common.Models;

namespace Banana.Backtest.Emulator.ExchangeEmulator;

public class OrderBookDataClass(int windowSize)
{
    private readonly Queue<double>[] _bidPricesEmaCollection = CreateEmaCollection(4, windowSize);
    private readonly Queue<double>[] _bidVolumesEmaCollection = CreateEmaCollection(4, windowSize);
    private readonly Queue<double>[] _askPricesEmaCollection = CreateEmaCollection(4, windowSize);
    private readonly Queue<double>[] _askVolumesEmaCollection = CreateEmaCollection(4, windowSize);
    private long _timestamp;

    public FeaturesClass Generate(OrderBookSnapshot snapshot)
    {
        var features = new FeaturesClass();
        features.PeriodMs = _timestamp == 0 ? 0 : snapshot.Timestamp - _timestamp;
        _timestamp = snapshot.Timestamp;
        Span<OrderBook.OrderBookLevel> bids = stackalloc OrderBook.OrderBookLevel[4];
        Span<OrderBook.OrderBookLevel> asks = stackalloc OrderBook.OrderBookLevel[4];
        snapshot.FillBids(bids, 4);
        snapshot.FillAsks(asks, 4);
        for (var i = 0; i < 4; i++)
        {
            UpdateEma(_bidPricesEmaCollection[i], bids[i].Price, windowSize);
            UpdateEma(_bidVolumesEmaCollection[i], bids[i].Price * bids[i].Quantity, windowSize);
            UpdateEma(_askPricesEmaCollection[i], asks[i].Price, windowSize);
            UpdateEma(_askVolumesEmaCollection[i], asks[i].Price * asks[i].Quantity, windowSize);
        }

        features.Bid1PriceMA = (float)_bidPricesEmaCollection[0].Last();
        features.Bid2PriceMA = (float)_bidPricesEmaCollection[1].Last();
        features.Bid3PriceMA = (float)_bidPricesEmaCollection[2].Last();
        features.Bid4PriceMA = (float)_bidPricesEmaCollection[3].Last();

        features.Bid1VolumeMA = (float)_bidVolumesEmaCollection[0].Last();
        features.Bid2VolumeMA = (float)_bidVolumesEmaCollection[1].Last();
        features.Bid3VolumeMA = (float)_bidVolumesEmaCollection[2].Last();
        features.Bid4VolumeMA = (float)_bidVolumesEmaCollection[3].Last();

        features.Ask1PriceMA = (float)_askPricesEmaCollection[0].Last();
        features.Ask2PriceMA = (float)_askPricesEmaCollection[1].Last();
        features.Ask3PriceMA = (float)_askPricesEmaCollection[2].Last();
        features.Ask4PriceMA = (float)_askPricesEmaCollection[3].Last();

        features.Ask1VolumeMA = (float)_askVolumesEmaCollection[0].Last();
        features.Ask2VolumeMA = (float)_askVolumesEmaCollection[1].Last();
        features.Ask3VolumeMA = (float)_askVolumesEmaCollection[2].Last();
        features.Ask4VolumeMA = (float)_askVolumesEmaCollection[3].Last();

        return features;
    }

    private static double UpdateEma(Queue<double>? history, double value, int size)
    {
        var alpha = 2.0 / (size + 1.0);

        if (history is null)
            history = new Queue<double>(size);

        if (history.Count == 0)
        {
            history.Enqueue(value);
            return value;
        }
        if (history.Count >= size)
            history.Dequeue();

        var previous = history.Last();
        var ema = alpha * value + (1 - alpha) * previous;
        history.Enqueue(ema);
        return ema;
    }

    private static Queue<double>[] CreateEmaCollection(int size, int window)
    {
        var array = new Queue<double>[size];
        for (var i = 0; i < size; i++)
        {
            array[i] = new Queue<double>(window);
        }

        return array;
    }
}
