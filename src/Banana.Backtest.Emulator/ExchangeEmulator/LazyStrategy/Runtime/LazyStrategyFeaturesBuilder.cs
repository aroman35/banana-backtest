using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Serilog;

namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy.Runtime;

public class LazyStrategyFeaturesBuilder(ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<LazyStrategyFeaturesBuilder>();
    private readonly TimeSpan _windowDuration = TimeSpan.FromSeconds(5);
    private DateTime _currentWindowStart = DateTime.MinValue;
    private OrderBookSnapshot? _latestOrderBook;

    // Накопленные данные по сделкам в текущем окне.
    private double _totalTradeVolume;
    private int _tradeCount;
    private double _buyTradeVolume;
    private double _sellTradeVolume;
    private double? _firstTradePrice;
    private double _lastTradePrice;

    /// <summary>
    /// Обрабатывает обновление стакана.
    /// </summary>
    /// <param name="orderBookSnapshot">Обновление стакана.</param>
    /// <param name="featureRecordMapped"></param>
    public bool OrderBookUpdated(MarketDataItem<OrderBookSnapshot> orderBookSnapshot, out ModelInput? featureRecordMapped)
    {
        var dt = orderBookSnapshot.DateTime;
        InitializeWindowIfNeeded(dt);

        featureRecordMapped = null;
        if (dt >= _currentWindowStart + _windowDuration)
        {
            FlushWindow(out featureRecordMapped);
            // Новое окно: округление до начала текущей минуты.
            _currentWindowStart = Floor(dt, _windowDuration);
        }

        _latestOrderBook = orderBookSnapshot.Item;
        _logger.Verbose("OrderBook updated at {Timestamp}", dt);
        return featureRecordMapped is not null;
    }

    /// <summary>
    /// Обрабатывает поступление информации о сделке.
    /// </summary>
    /// <param name="trade">Информация о сделке.</param>
    /// <param name="featureRecordMapped"></param>
    public bool AnonymousTradeReceived(MarketDataItem<TradeUpdate> trade, out ModelInput? featureRecordMapped)
    {
        var dt = trade.DateTime;
        InitializeWindowIfNeeded(dt);
        featureRecordMapped = null;

        if (dt >= _currentWindowStart + _windowDuration)
        {
            FlushWindow(out featureRecordMapped);
            _currentWindowStart = Floor(dt, _windowDuration);
        }

        var tradeUpdate = trade.Item;
        var tradeVolume = tradeUpdate.Volume;
        _totalTradeVolume += tradeVolume;
        _tradeCount++;
        if (tradeUpdate.IsBuyer)
        {
            _buyTradeVolume += tradeVolume;
        }
        else
        {
            _sellTradeVolume += tradeVolume;
        }

        if (_firstTradePrice == null)
        {
            _firstTradePrice = tradeUpdate.Price;
        }

        _lastTradePrice = tradeUpdate.Price;
        _logger.Verbose("Trade received at {Timestamp}: {TradeInfo}", dt, tradeUpdate.ToString());
        return featureRecordMapped is not null;
    }

    /// <summary>
    /// Инициализирует окно агрегации, если оно ещё не задано.
    /// </summary>
    /// <param name="dt">Время события.</param>
    private void InitializeWindowIfNeeded(DateTime dt)
    {
        if (_currentWindowStart == DateTime.MinValue)
        {
            _currentWindowStart = Floor(dt, _windowDuration);
            _logger.Verbose("Window initialized at {WindowStart}", _currentWindowStart);
        }
    }

    /// <summary>
    /// Сбрасывает накопленные данные по сделкам и формирует целевую фичу в формате <see cref="FeatureRecordMapped"/> для текущего окна.
    /// </summary>
    private void FlushWindow(out ModelInput? record)
    {
        record = null;
        if (_latestOrderBook == null)
        {
            ResetTradeAggregation();
            _logger.Warning("FlushWindow skipped: no order book available");
            return;
        }

        // Создаем целевую фичу напрямую.
        record = new ModelInput
        {
            BestBid = (float)_latestOrderBook.Value.Bid(0).Price,
            BestAsk = (float)_latestOrderBook.Value.Ask(0).Price,
            Spread = (float)(_latestOrderBook.Value.Ask(0).Price - _latestOrderBook.Value.Bid(0).Price)
        };

        // Вычисляем MidPrice и SpreadPct.
        record.MidPrice = (record.BestBid + record.BestAsk) / 2;
        record.SpreadPct = record.MidPrice != 0 ? record.Spread / record.MidPrice : 0;

        // Вычисляем VWAP и объемы.
        double bidVolume = 0;
        double askVolume = 0;
        double vwapBidNumerator = 0;
        double vwapAskNumerator = 0;
        for (var i = 0; i < OrderBookSnapshot.Depth; i++)
        {
            var bid = _latestOrderBook.Value.Bid(i);
            var ask = _latestOrderBook.Value.Ask(i);
            bidVolume += bid.Quantity;
            askVolume += ask.Quantity;
            vwapBidNumerator += bid.Price * bid.Quantity;
            vwapAskNumerator += ask.Price * ask.Quantity;
        }

        var totalVolume = bidVolume + askVolume;
        record.Imbalance = totalVolume != 0 ? (float)((bidVolume - askVolume) / totalVolume) : 0;
        record.VWAPBid = bidVolume > 0 ? (float)(vwapBidNumerator / bidVolume) : 0;
        record.VWAPAsk = askVolume > 0 ? (float)(vwapAskNumerator / askVolume) : 0;

        // Записываем данные по сделкам.
        record.TotalTradeVolume = (float)_totalTradeVolume;
        record.TradeCount = _tradeCount;
        record.BuyTradeVolume = (float)_buyTradeVolume;
        record.SellTradeVolume = (float)_sellTradeVolume;
        record.TradePriceChange = _firstTradePrice.HasValue ? (float)(_lastTradePrice - _firstTradePrice.Value) : 0;

        // Вычисляем производные признаки.
        record.PriceChangePct = record.MidPrice != 0 ? record.TradePriceChange / record.MidPrice : 0;
        record.TradeIntensity = _windowDuration.TotalSeconds > 0
            ? _tradeCount / (float)_windowDuration.TotalSeconds
            : 0;

        _logger.Verbose("Flushed window at {Timestamp}. Total features count: {Count}", _currentWindowStart, _tradeCount);

        ResetTradeAggregation();
    }

    /// <summary>
    /// Сбрасывает накопленные данные по сделкам.
    /// </summary>
    private void ResetTradeAggregation()
    {
        _totalTradeVolume = 0;
        _tradeCount = 0;
        _buyTradeVolume = 0;
        _sellTradeVolume = 0;
        _firstTradePrice = null;
        _lastTradePrice = 0;
    }

    /// <summary>
    /// Сбрасывает оставшиеся накопленные данные (например, в конце дня).
    /// </summary>
    public void FlushRemaining()
    {
        FlushWindow(out _);
        _logger.Information("Remaining data flushed");
    }

    private DateTime Floor(DateTime dateTime, TimeSpan interval)
    {
        if (interval.Ticks <= 0)
            throw new ArgumentException("Interval must be greater than zero.", nameof(interval));

        var flooredTicks = (dateTime.Ticks / interval.Ticks) * interval.Ticks;
        return new DateTime(flooredTicks, dateTime.Kind);
    }
}
