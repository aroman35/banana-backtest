using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Npgsql;
using Serilog;

namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy;

    /// <summary>
    /// Собирает целевые фичи (в формате <see cref="FeatureRecordMapped"/>) на основе обновлений стакана и потока сделок.
    /// Реализует интерфейс <see cref="IEmulatorGateway"/>.
    /// Фичи накапливаются в приватном поле <c>_features</c>, которое сразу содержит данные в требуемом формате.
    /// </summary>
    public class FeatureBuilder : IEmulatorGateway
    {
        private readonly ILogger _logger;
        private readonly TimeSpan _windowDuration = TimeSpan.FromSeconds(15);
        private readonly List<FeatureRecordMapped> _features = new List<FeatureRecordMapped>();
        private readonly FeatureRepository _featureRepository;
        public IReadOnlyList<FeatureRecordMapped> Features => _features;
        private DateTime _currentWindowStart = DateTime.MinValue;
        private OrderBookSnapshot? _latestOrderBook = null;

        // Накопленные данные по сделкам в текущем окне.
        private double _totalTradeVolume = 0;
        private int _tradeCount = 0;
        private double _buyTradeVolume = 0;
        private double _sellTradeVolume = 0;
        private double? _firstTradePrice = null;
        private double _lastTradePrice = 0;

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="FeatureBuilder"/>.
        /// </summary>
        /// <param name="logger">Экземпляр <see cref="ILogger"/> для логирования.</param>
        public FeatureBuilder(FeatureRepository featureRepository, ILogger logger)
        {
            _logger = logger.ForContext<FeatureBuilder>();
            _featureRepository = featureRepository;
            _logger.Information("FeatureBuilder initialized with window duration {WindowDuration}.", _windowDuration);
        }

        /// <summary>
        /// Обрабатывает обновление стакана.
        /// </summary>
        /// <param name="orderBookSnapshot">Обновление стакана.</param>
        public void OrderBookUpdated(MarketDataItem<OrderBookSnapshot> orderBookSnapshot)
        {
            DateTime dt = orderBookSnapshot.DateTime;
            InitializeWindowIfNeeded(dt);

            if (dt >= _currentWindowStart + _windowDuration)
            {
                FlushWindow();
                // Новое окно: округление до начала текущей минуты.
                _currentWindowStart = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);
            }

            _latestOrderBook = orderBookSnapshot.Item;
            _logger.Debug("OrderBook updated at {Timestamp}.", dt);
        }

        /// <summary>
        /// Обрабатывает поступление информации о сделке.
        /// </summary>
        /// <param name="trade">Информация о сделке.</param>
        public void AnonymousTradeReceived(MarketDataItem<TradeUpdate> trade)
        {
            DateTime dt = trade.DateTime;
            InitializeWindowIfNeeded(dt);

            if (dt >= _currentWindowStart + _windowDuration)
            {
                FlushWindow();
                _currentWindowStart = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);
            }

            var tradeUpdate = trade.Item;
            double tradeVolume = tradeUpdate.Volume;
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
            _logger.Debug("Trade received at {Timestamp}: {TradeInfo}.", dt, tradeUpdate.ToString());
        }

        /// <summary>
        /// Обрабатывает информацию о пользовательском исполнении.
        /// </summary>
        /// <param name="userExecution">Информация о пользовательском исполнении.</param>
        public void UserExecutionReceived(UserExecution userExecution)
        {
            throw new NotImplementedException("User execution processing is not implemented.");
        }

        /// <summary>
        /// Инициализирует окно агрегации, если оно ещё не задано.
        /// </summary>
        /// <param name="dt">Время события.</param>
        private void InitializeWindowIfNeeded(DateTime dt)
        {
            if (_currentWindowStart == DateTime.MinValue)
            {
                _currentWindowStart = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);
                _logger.Debug("Window initialized at {WindowStart}.", _currentWindowStart);
            }
        }

        /// <summary>
        /// Сбрасывает накопленные данные по сделкам и формирует целевую фичу в формате <see cref="FeatureRecordMapped"/> для текущего окна.
        /// </summary>
        private unsafe void FlushWindow()
        {
            if (_latestOrderBook == null)
            {
                ResetTradeAggregation();
                _logger.Warning("FlushWindow skipped: no order book available.");
                return;
            }

            // Создаем целевую фичу напрямую.
            var record = new FeatureRecordMapped
            {
                Timestamp = _currentWindowStart,
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
            for (int i = 0; i < OrderBookSnapshot.Depth; i++)
            {
                var bid = _latestOrderBook.Value.Bid(i);
                var ask = _latestOrderBook.Value.Ask(i);
                bidVolume += bid.Quantity;
                askVolume += ask.Quantity;
                vwapBidNumerator += bid.Price * bid.Quantity;
                vwapAskNumerator += ask.Price * ask.Quantity;
            }

            double totalVolume = bidVolume + askVolume;
            record.Imbalance = totalVolume != 0 ? (float)((bidVolume - askVolume) / totalVolume) : 0;
            record.VWAPBid = bidVolume > 0 ? (float)(vwapBidNumerator / bidVolume) : 0;
            record.VWAPAsk = askVolume > 0 ? (float)(vwapAskNumerator / askVolume) : 0;

            // Записываем данные по сделкам.
            record.TotalTradeVolume = (float)_totalTradeVolume;
            record.TradeCount = (float)_tradeCount;
            record.BuyTradeVolume = (float)_buyTradeVolume;
            record.SellTradeVolume = (float)_sellTradeVolume;
            record.TradePriceChange = _firstTradePrice.HasValue ? (float)(_lastTradePrice - _firstTradePrice.Value) : 0;

            // Вычисляем производные признаки.
            record.PriceChangePct = record.MidPrice != 0 ? record.TradePriceChange / record.MidPrice : 0;
            record.TradeIntensity = _windowDuration.TotalSeconds > 0 ? (float)_tradeCount / (float)_windowDuration.TotalSeconds : 0;

            // Начальное значение метки устанавливаем в 0 (вероятность смены тренда будет рассчитана позже)
            record.Label = 0;

            _features.Add(record);
            _logger.Debug("Flushed window at {Timestamp}. Total features count: {Count}.", _currentWindowStart, _features.Count);

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
            FlushWindow();
            _logger.Information("Remaining data flushed.");
        }

        /// <summary>
        /// Сохраняет собранные целевые фичи (<see cref="FeatureRecordMapped"/>) в указанную таблицу PostgreSQL.
        /// Сохраняются также параметры разметки и тикер инструмента.
        /// </summary>
        /// <param name="connectionString">Строка подключения к PostgreSQL.</param>
        /// <param name="tableName">Имя таблицы для сохранения фич.</param>
        /// <param name="ticker">Тикер инструмента.</param>
         public void SaveFeaturesToPostgreSQL(string connectionString, string tableName, string ticker)
        {
            _logger.Information("Saving target features for ticker {Ticker} to table {Table}...", ticker, tableName);
            _featureRepository.BulkInsertFeatures(_features);
            _logger.Information("Target features saved successfully for ticker {Ticker}.", ticker);
        }
    }
