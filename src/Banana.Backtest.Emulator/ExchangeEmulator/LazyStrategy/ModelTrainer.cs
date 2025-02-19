using Microsoft.ML;
using Microsoft.ML.Data;
using Npgsql;
using Serilog;

namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy;

    /// <summary>
    /// ModelTrainer загружает целевые фичи (FeatureRecordMapped) из БД, обучает регрессионную модель
    /// для предсказания вероятности смены тренда и сохраняет обновленные фичи в БД
    /// </summary>
    public class ModelTrainer
    {
        private readonly ILogger _logger;
        private readonly FeatureRepository _featureRepository;
        private readonly string _connectionString;
        private readonly MLContext _mlContext;
        private readonly double _thresholdPercent;
        private readonly int _windowsRange;
        private readonly double _sigma;
        private readonly string _featuresTable;

        /// <summary>
        /// Приватное поле для хранения целевых фич, загруженных из БД
        /// </summary>
        private List<FeatureRecordMapped> _features;

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="ModelTrainer"/> и загружает фичи из БД
        /// </summary>
        /// <param name="connectionString">
        /// Строка подключения к PostgreSQL
        /// </param>
        /// <param name="featuresTable">
        /// Имя таблицы с целевыми фичами
        /// </param>
        /// <param name="thresholdPercent">
        /// Порог для обнаружения разворота (например, 0.005 для 0.5% изменения)
        /// </param>
        /// <param name="sigma">
        /// Параметр гауссовой функции для расчёта вероятности
        /// </param>
        /// <param name="windowsRange">
        /// Количество окон до и после точки разворота для разметки
        /// </param>
        /// <param name="logger">
        /// Экземпляр <see cref="ILogger"/> для логирования
        /// </param>
        public ModelTrainer(
            string connectionString,
            string featuresTable,
            double thresholdPercent,
            double sigma,
            int windowsRange,
            ILogger logger)
        {
            _connectionString = connectionString;
            _featuresTable = featuresTable;
            _thresholdPercent = thresholdPercent;
            _sigma = sigma;
            _windowsRange = windowsRange;
            _featureRepository = new FeatureRepository(connectionString, featuresTable, "NG", logger);
            _mlContext = new MLContext(seed: 0);
            _logger = logger.ForContext<ModelTrainer>();

            _logger.Information(
                "Initializing ModelTrainer" +
                "\nTable: {Table}" +
                "\nThresholdPercent: {ThresholdPercent}" +
                "\nSigma: {Sigma}" +
                "\nWindowsRange: {WindowsRange}",
                featuresTable,
                thresholdPercent,
                sigma,
                windowsRange);

            _features = LoadFeaturesFromPostgreSQL();
            _logger.Information("Loaded {Count} features from DB", _features.Count);
        }

        /// <summary>
        /// Загружает фичи из таблицы PostgreSQL, содержащей данные в формате FeatureRecordMapped
        /// </summary>
        /// <returns>
        /// Список объектов <see cref="FeatureRecordMapped"/>
        /// </returns>
        private List<FeatureRecordMapped> LoadFeaturesFromPostgreSQL()
        {
            var features = new List<FeatureRecordMapped>();
            _logger.Information("Loading features from table {Table}", _featuresTable);

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand(
                    @$"SELECT 
                        timestamp,
                        bestbid,
                        bestask,
                        spread,
                        imbalance,
                        vwapbid,
                        vwapask,
                        totaltradevolume,
                        tradecount,
                        buytradevolume,
                        selltradevolume,
                        tradepricechange,
                        midprice,
                        spreadpct,
                        pricechangepct,
                        tradeintensity,
                        label
                      FROM {_featuresTable}", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            features.Add(new FeatureRecordMapped
                            {
                                Timestamp = reader.GetDateTime(0),
                                BestBid = reader.GetFloat(1),
                                BestAsk = reader.GetFloat(2),
                                Spread = reader.GetFloat(3),
                                Imbalance = reader.IsDBNull(4) ? 0 : reader.GetFloat(4),
                                VWAPBid = reader.GetFloat(5),
                                VWAPAsk = reader.GetFloat(6),
                                TotalTradeVolume = reader.GetFloat(7),
                                TradeCount = reader.GetFloat(8),
                                BuyTradeVolume = reader.GetFloat(9),
                                SellTradeVolume = reader.GetFloat(10),
                                TradePriceChange = reader.GetFloat(11),
                                MidPrice = reader.GetFloat(12),
                                SpreadPct = reader.GetFloat(13),
                                PriceChangePct = reader.GetFloat(14),
                                TradeIntensity = reader.GetFloat(15),
                                Label = reader.GetFloat(16)
                            });
                        }
                    }
                }
            }

            _logger.Information("Loaded {Count} features", features.Count);
            return features;
        }

        /// <summary>
        /// Переразмечает загруженные фичи, назначая каждой вероятность смены тренда,
        /// используя гауссовскую функцию: p = exp( - (distance^2) / (2 * sigma^2) )
        /// </summary>
        public void ReLabelFeaturesWithProbability()
        {
            _logger.Information(
                "Re-labeling features with probability using" +
                "\nThresholdPercent: {ThresholdPercent}" +
                "\nSigma: {Sigma}" +
                "\nWindowsRange: {WindowsRange}",
                _thresholdPercent,
                _sigma,
                _windowsRange);

            // Используем float[] для цен, так как BestBid и BestAsk имеют тип float
            float[] prices = _features.Select(r => (r.BestBid + r.BestAsk) / 2).ToArray();
            List<int> reversalIndices = FindTrendReversalIndices(prices, _thresholdPercent);
            _logger.Information("Detected {Count} trend reversal points", reversalIndices.Count);

            foreach (var feature in _features)
            {
                feature.Label = 0;
            }

            foreach (int i in reversalIndices)
            {
                int start = Math.Max(0, i - _windowsRange);
                int end = Math.Min(_features.Count - 1, i + _windowsRange);
                for (int j = start; j <= end; j++)
                {
                    float distance = Math.Abs(j - i);
                    // Используем MathF.Exp для вычисления экспоненты с типом float
                    float p = MathF.Exp(- (distance * distance) / (2 * (float)_sigma * (float)_sigma));
                    _features[j].Label = Math.Max(_features[j].Label, p);
                }
            }

            _logger.Information("Re-labeling complete");
        }

        /// <summary>
        /// Определяет индексы точек разворота в массиве цен на основе заданного порога
        /// </summary>
        /// <param name="prices">Массив цен (float[])</param>
        /// <param name="thresholdPercent">Порог для обнаружения разворота</param>
        /// <returns>Список индексов точек разворота</returns>
        private static List<int> FindTrendReversalIndices(
            float[] prices,
            double thresholdPercent)
        {
            var reversalIndices = new List<int>();

            if (prices == null || prices.Length < 2)
            {
                return reversalIndices;
            }

            int lastExtremeIndex = 0;
            // Приводим первый элемент к double для точности сравнения
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

        /// <summary>
        /// Сохраняет обновленные целевые фичи в указанную таблицу PostgreSQL.
        /// Перед сохранением удаляются старые записи для заданного тикера
        /// </summary>
        /// <param name="connectionString">
        /// Строка подключения к PostgreSQL
        /// </param>
        /// <param name="tableName">
        /// Имя таблицы для сохранения фич
        /// </param>
        /// <param name="ticker">
        /// Тикер инструмента
        /// </param>
        public void SaveFeaturesToPostgreSQL(string ticker)
        {
            _logger.Information(
                "Saving target features for ticker {Ticker}",
                ticker);
            _featureRepository.UpdateFeatures(_features, "NG_Labeled");
            _logger.Information("Target features saved successfully for ticker {Ticker}", ticker);
        }

        /// <summary>
        /// Обучает регрессионную модель (LightGbm) для предсказания вероятности смены тренда
        /// на основе целевых фич, загруженных из БД.
        /// </summary>
        /// <returns>
        /// Кортеж, содержащий обученную модель и метрики регрессии,
        /// с именованными параметрами: (ITransformer Model, RegressionMetrics Metrics)
        /// </returns>
        public (ITransformer Model, RegressionMetrics Metrics) TrainModel()
        {
            _logger.Information("Starting model training");
            ReLabelFeaturesWithProbability();
            var dataView = _mlContext.Data.LoadFromEnumerable(_features);

            var split = _mlContext.Data.TrainTestSplit(
                dataView,
                testFraction: 0.2);
            var trainData = split.TrainSet;
            var testData = split.TestSet;

            var pipeline = _mlContext.Transforms.Concatenate(
                "Features",
                nameof(FeatureRecordMapped.BestBid),
                nameof(FeatureRecordMapped.BestAsk),
                nameof(FeatureRecordMapped.Spread),
                nameof(FeatureRecordMapped.Imbalance),
                nameof(FeatureRecordMapped.VWAPBid),
                nameof(FeatureRecordMapped.VWAPAsk),
                nameof(FeatureRecordMapped.TotalTradeVolume),
                nameof(FeatureRecordMapped.TradeCount),
                nameof(FeatureRecordMapped.BuyTradeVolume),
                nameof(FeatureRecordMapped.SellTradeVolume),
                nameof(FeatureRecordMapped.TradePriceChange),
                nameof(FeatureRecordMapped.MidPrice),
                nameof(FeatureRecordMapped.SpreadPct),
                nameof(FeatureRecordMapped.PriceChangePct),
                nameof(FeatureRecordMapped.TradeIntensity))
                .Append(_mlContext.Regression.Trainers.LightGbm(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    numberOfIterations: 100));

            var model = pipeline.Fit(trainData);
            var predictions = model.Transform(testData);
            var metrics = _mlContext.Regression.Evaluate(
                predictions,
                labelColumnName: "Label",
                scoreColumnName: "Score");

            _logger.Information(
                "Model training complete. Metrics: R²={RSquared:P2}, RMSE={RMSE:F4}, MAE={MAE:F4}",
                metrics.RSquared,
                metrics.RootMeanSquaredError,
                metrics.MeanAbsoluteError);

            return (Model: model, Metrics: metrics);
        }
    }
