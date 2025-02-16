using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Npgsql;
using Serilog;

namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy;

public class FeatureBuilder(ILogger logger) : IEmulatorGateway
{
    private readonly ILogger _logger = logger.ForContext<FeatureBuilder>();
    // Интервал агрегации (здесь 1 минута)
    private readonly TimeSpan _windowDuration = TimeSpan.FromSeconds(60);

    // Список сформированных фичей
    private readonly List<FeatureRecord> _features = new List<FeatureRecord>();
    public IReadOnlyList<FeatureRecord> Features => _features;

    // Начало текущего окна агрегации
    private DateTime _currentWindowStart = DateTime.MinValue;

    // Храним последний полученный стакан для вычисления признаков
    private OrderBookSnapshot? _latestOrderBook = null;

    // Накопление данных по сделкам в текущем окне
    private double _totalTradeVolume = 0;
    private int _tradeCount = 0;
    private double _buyTradeVolume = 0;
    private double _sellTradeVolume = 0;
    private double? _firstTradePrice = null;
    private double _lastTradePrice = 0;

    // Вызывается при обновлении стакана
    public void OrderBookUpdated(MarketDataItem<OrderBookSnapshot> orderBookSnapshot)
    {
        var dt = orderBookSnapshot.DateTime;
        InitializeWindowIfNeeded(dt);

        // Если событие выходит за пределы текущего окна, сбрасываем накопленные данные
        if (dt >= _currentWindowStart + _windowDuration)
        {
            FlushWindow();
            // Начинаем новое окно – округляем время до начала текущей минуты
            _currentWindowStart = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);
        }

        // Сохраняем последний стакан (на данный момент)
        _latestOrderBook = orderBookSnapshot.Item;
    }

    // Вызывается при поступлении информации о сделке
    public void AnonymousTradeReceived(MarketDataItem<TradeUpdate> trade)
    {
        var dt = trade.DateTime;
        InitializeWindowIfNeeded(dt);

        if (dt >= _currentWindowStart + _windowDuration)
        {
            FlushWindow();
            _currentWindowStart = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);
        }

        // Агрегируем данные по сделкам
        var tradeUpdate = trade.Item;
        var tradeVolume = tradeUpdate.Volume;
        _totalTradeVolume += tradeVolume;
        _tradeCount++;
        if (tradeUpdate.IsBuyer)
            _buyTradeVolume += tradeVolume;
        else
            _sellTradeVolume += tradeVolume;

        if (_firstTradePrice == null)
            _firstTradePrice = tradeUpdate.Price;
        _lastTradePrice = tradeUpdate.Price;
    }

    // Заглушка для пользовательского исполнения
    public void UserExecutionReceived(UserExecution userExecution)
    {
        throw new NotImplementedException();
    }

    // Инициализация окна агрегации, если оно ещё не задано
    private void InitializeWindowIfNeeded(DateTime dt)
    {
        if (_currentWindowStart == DateTime.MinValue)
        {
            // Начало окна – округление до начала минуты
            _currentWindowStart = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);
        }
    }

    // Функция сброса накопленных данных текущего окна и формирования FeatureRecord
    private unsafe void FlushWindow()
    {
        // Если стакан не получен, пропускаем формирование фич для данного окна
        if (_latestOrderBook == null)
        {
            ResetTradeAggregation();
            return;
        }

        var record = new FeatureRecord
        {
            Timestamp = _currentWindowStart
        };

        // Извлекаем признаки из стакана: лучшие цены
        var bestBid = _latestOrderBook.Value.Bid(0).Price;
        var bestAsk = _latestOrderBook.Value.Ask(0).Price;
        record.BestBid = bestBid;
        record.BestAsk = bestAsk;
        record.Spread = bestAsk - bestBid;

        // Вычисляем новые признаки на основе лучших цен
        record.MidPrice = (bestBid + bestAsk) / 2;
        record.SpreadPct = record.MidPrice != 0 ? record.Spread / record.MidPrice : 0;

        // Считаем суммарные объемы и VWAP для бидов и асков
        double bidVolume = 0;
        double askVolume = 0;
        double vwapBidNumerator = 0;
        double vwapAskNumerator = 0;
        for (var i = 0; i < OrderBookSnapshot.Depth; i++)
        {
            var bid = _latestOrderBook.Value.Bid(i);
            var ask = _latestOrderBook.Value.Ask(i);
            var bidPrice = bid.Price;
            var bidQuantity = bid.Quantity;
            var askPrice = ask.Price;
            var askQuantity = ask.Quantity;

            bidVolume += bidQuantity;
            askVolume += askQuantity;
            vwapBidNumerator += bidPrice * bidQuantity;
            vwapAskNumerator += askPrice * askQuantity;
        }
        // Имбаланс – нормированная разность объемов (проверяем, чтобы не было деления на 0)
        double totalVolume = bidVolume + askVolume;
        record.Imbalance = totalVolume != 0 ? (bidVolume - askVolume) / totalVolume : 0;
        record.VWAPBid = bidVolume > 0 ? vwapBidNumerator / bidVolume : 0;
        record.VWAPAsk = askVolume > 0 ? vwapAskNumerator / askVolume : 0;

        // Записываем признаки по сделкам
        record.TotalTradeVolume = _totalTradeVolume;
        record.TradeCount = _tradeCount;
        record.BuyTradeVolume = _buyTradeVolume;
        record.SellTradeVolume = _sellTradeVolume;
        record.TradePriceChange = _firstTradePrice.HasValue ? _lastTradePrice - _firstTradePrice.Value : 0;

        // Новые признаки на основе сделок:
        // Относительное изменение цены (если MidPrice != 0)
        record.PriceChangePct = record.MidPrice != 0 ? record.TradePriceChange / record.MidPrice : 0;
        // Интенсивность сделок: число сделок в секунду (окно задается _windowDuration)
        record.TradeIntensity = _windowDuration.TotalSeconds > 0 ? _tradeCount / _windowDuration.TotalSeconds : 0;

        // Добавляем запись в список фич
        _features.Add(record);

        // Сбрасываем агрегацию по сделкам для нового окна
        ResetTradeAggregation();
    }

    // Сброс накопленных данных по сделкам
    private void ResetTradeAggregation()
    {
        _totalTradeVolume = 0;
        _tradeCount = 0;
        _buyTradeVolume = 0;
        _sellTradeVolume = 0;
        _firstTradePrice = null;
        _lastTradePrice = 0;
    }

    // Метод для сброса оставшихся накопленных данных в конце обработки дня
    public void FlushRemaining()
    {
        FlushWindow();
    }

    // Функция поиска точек смены тренда (ваш алгоритм)
    private static List<int> FindTrendReversalIndices(double[] prices, double thresholdPercent)
    {
        List<int> reversalIndices = new List<int>();

        if (prices == null || prices.Length < 2)
            return reversalIndices;

        int lastExtremeIndex = 0;
        double lastExtremePrice = prices[0];
        reversalIndices.Add(lastExtremeIndex);

        int currentTrend = 0;

        for (int i = 1; i < prices.Length; i++)
        {
            double price = prices[i];

            if (currentTrend == 0)
            {
                if (price >= lastExtremePrice * (1 + thresholdPercent))
                {
                    currentTrend = 1;
                    lastExtremeIndex = i;
                    lastExtremePrice = price;
                    reversalIndices.Add(i);
                }
                else if (price <= lastExtremePrice * (1 - thresholdPercent))
                {
                    currentTrend = -1;
                    lastExtremeIndex = i;
                    lastExtremePrice = price;
                    reversalIndices.Add(i);
                }
            }
            else if (currentTrend == 1)
            {
                if (price > lastExtremePrice)
                {
                    lastExtremeIndex = i;
                    lastExtremePrice = price;
                    reversalIndices[reversalIndices.Count - 1] = i;
                }
                else if (price <= lastExtremePrice * (1 - thresholdPercent))
                {
                    currentTrend = -1;
                    lastExtremeIndex = i;
                    lastExtremePrice = price;
                    reversalIndices.Add(i);
                }
            }
            else if (currentTrend == -1)
            {
                if (price < lastExtremePrice)
                {
                    lastExtremeIndex = i;
                    lastExtremePrice = price;
                    reversalIndices[reversalIndices.Count - 1] = i;
                }
                else if (price >= lastExtremePrice * (1 + thresholdPercent))
                {
                    currentTrend = 1;
                    lastExtremeIndex = i;
                    lastExtremePrice = price;
                    reversalIndices.Add(i);
                }
            }
        }

        return reversalIndices;
    }

    // После завершения сбора фич (например, в конце дня)
    public void LabelFeatures(double thresholdPercent)
    {
        // Извлекаем массив цен. Здесь можно взять среднюю цену стакана:
        double[] prices = _features
            .Select(r => (r.BestBid + r.BestAsk) / 2)
            .ToArray();

        // Находим индексы точек смены тренда
        List<int> reversalIndices = FindTrendReversalIndices(prices, thresholdPercent);

        // Изначально считаем, что в окне нет разворота
        foreach (var record in _features)
        {
            record.Label = 0;
        }

        // Для найденных индексов ставим метку разворота (Label = 1)
        foreach (int idx in reversalIndices)
        {
            if (idx >= 0 && idx < _features.Count)
            {
                _features[idx].Label = 1;
            }
        }
        _logger.Information("Labeling features complete");
    }

    public void RemoveEmptyFeatures()
    {
        _features.RemoveAll(feature =>
            feature.TotalTradeVolume == 0 ||
            (feature.BestBid == 0 &&
             feature.BestAsk == 0 &&
             feature.Spread == 0 &&
             (double.IsNaN(feature.Imbalance) || feature.Imbalance == 0) &&
             feature.VWAPBid == 0 &&
             feature.VWAPAsk == 0 &&
             feature.TotalTradeVolume == 0 &&
             feature.TradeCount == 0 &&
             feature.BuyTradeVolume == 0 &&
             feature.SellTradeVolume == 0 &&
             feature.TradePriceChange == 0));
    }

    public void SaveFeaturesToPostgreSql(string connectionString)
    {
        using (var connection = new NpgsqlConnection(connectionString))
        {
            connection.Open();
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var feature in _features)
                {
                    using (var command = new NpgsqlCommand(
                        @"INSERT INTO features_ng_old (
                            timestamp, bestbid, bestask, spread, imbalance,
                            vwapbid, vwapask, totaltradevolume, tradecount, 
                            buytradevolume, selltradevolume, tradepricechange,
                            midprice, spreadpct, pricechangepct, tradeintensity, label
                        ) VALUES (
                            @timestamp, @bestbid, @bestask, @spread, @imbalance,
                            @vwapbid, @vwapask, @totaltradevolume, @tradecount, 
                            @buytradevolume, @selltradevolume, @tradepricechange,
                            @midprice, @spreadpct, @pricechangepct, @tradeintensity, @label
                        )",
                        connection))
                    {
                        command.Parameters.AddWithValue("@timestamp", feature.Timestamp);
                        command.Parameters.AddWithValue("@bestbid", feature.BestBid);
                        command.Parameters.AddWithValue("@bestask", feature.BestAsk);
                        command.Parameters.AddWithValue("@spread", feature.Spread);

                        // Если Imbalance равен NaN, передаём DBNull
                        if (double.IsNaN(feature.Imbalance))
                            command.Parameters.AddWithValue("@imbalance", DBNull.Value);
                        else
                            command.Parameters.AddWithValue("@imbalance", feature.Imbalance);

                        command.Parameters.AddWithValue("@vwapbid", feature.VWAPBid);
                        command.Parameters.AddWithValue("@vwapask", feature.VWAPAsk);
                        command.Parameters.AddWithValue("@totaltradevolume", feature.TotalTradeVolume);
                        command.Parameters.AddWithValue("@tradecount", feature.TradeCount);
                        command.Parameters.AddWithValue("@buytradevolume", feature.BuyTradeVolume);
                        command.Parameters.AddWithValue("@selltradevolume", feature.SellTradeVolume);
                        command.Parameters.AddWithValue("@tradepricechange", feature.TradePriceChange);
                        command.Parameters.AddWithValue("@midprice", feature.MidPrice);
                        command.Parameters.AddWithValue("@spreadpct", feature.SpreadPct);
                        command.Parameters.AddWithValue("@pricechangepct", feature.PriceChangePct);
                        command.Parameters.AddWithValue("@tradeintensity", feature.TradeIntensity);
                        command.Parameters.AddWithValue("@label", feature.Label);

                        command.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
        }
    }
}
