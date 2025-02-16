using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.FastTree;
using Npgsql;
using Serilog;

namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy;

public class ModelTrainer
{
    private readonly ILogger _logger;
    private readonly string _connectionString;
    private readonly MLContext _mlContext;
    private readonly double _thresholdPercent;
    private readonly int _windowOffset;

    public ModelTrainer(string connectionString, double thresholdPercent, int windowOffset, ILogger logger)
    {
        _connectionString = connectionString;
        _thresholdPercent = thresholdPercent;
        _windowOffset = windowOffset;
        _mlContext = new MLContext(seed: 0);
        _logger = logger.ForContext<ModelTrainer>();
    }

    /// <summary>
    /// Загружает данные фич из PostgreSQL и возвращает их в виде списка FeatureRecord.
    /// </summary>
    public List<FeatureRecord> LoadFeaturesFromPostgreSQL(string tableName)
    {
        var features = new List<FeatureRecord>();

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            connection.Open();
            using (var command = new NpgsqlCommand(
                       @$"SELECT 
                    timestamp, bestbid, bestask, spread, imbalance, 
                    vwapbid, vwapask, totaltradevolume, tradecount, 
                    buytradevolume, selltradevolume, tradepricechange,
                    midprice, spreadpct, pricechangepct, tradeintensity, label 
                  FROM {tableName}",
                       connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var feature = new FeatureRecord
                        {
                            Timestamp = reader.GetDateTime(0),
                            BestBid = reader.GetDouble(1),
                            BestAsk = reader.GetDouble(2),
                            Spread = reader.GetDouble(3),
                            Imbalance = reader.IsDBNull(4) ? double.NaN : reader.GetDouble(4),
                            VWAPBid = reader.GetDouble(5),
                            VWAPAsk = reader.GetDouble(6),
                            TotalTradeVolume = reader.GetDouble(7),
                            TradeCount = reader.GetInt32(8),
                            BuyTradeVolume = reader.GetDouble(9),
                            SellTradeVolume = reader.GetDouble(10),
                            TradePriceChange = reader.GetDouble(11),
                            MidPrice = reader.GetDouble(12),
                            SpreadPct = reader.GetDouble(13),
                            PriceChangePct = reader.GetDouble(14),
                            TradeIntensity = reader.GetDouble(15),
                            Label = (float)reader.GetDouble(16)
                        };
                        features.Add(feature);
                    }
                }
            }
        }

        var windowOffset = 2;
        ReLabelFeatures(features, _thresholdPercent, _windowOffset);
        return features;
    }

    public List<FeatureRecordWithWeight> GetWeightedFeatures(List<FeatureRecordMapped> mappedFeatures)
    {
        // Подсчитываем количество примеров для каждого класса
        var totalNegatives = mappedFeatures.Count(f => f.Label == false);
        var totalPositives = mappedFeatures.Count(f => f.Label == true);

        // Если позитивных примеров мало, то их вес можно задать как отношение количества негативных к позитивным
        var positiveWeight = totalPositives > 0 ? (float)totalNegatives / totalPositives : 1f;

        var weightedFeatures = mappedFeatures.Select(f => new FeatureRecordWithWeight
        {
            Timestamp = f.Timestamp,
            BestBid = f.BestBid,
            BestAsk = f.BestAsk,
            Spread = f.Spread,
            Imbalance = f.Imbalance,
            VWAPBid = f.VWAPBid,
            VWAPAsk = f.VWAPAsk,
            TotalTradeVolume = f.TotalTradeVolume,
            TradeCount = f.TradeCount,
            BuyTradeVolume = f.BuyTradeVolume,
            SellTradeVolume = f.SellTradeVolume,
            TradePriceChange = f.TradePriceChange,
            MidPrice = f.MidPrice,
            SpreadPct = f.SpreadPct,
            PriceChangePct = f.PriceChangePct,
            TradeIntensity = f.TradeIntensity,
            Label = f.Label,
            Weight = f.Label ? positiveWeight : 1f
        }).ToList();

        return weightedFeatures;
    }

    public List<FeatureRecordMapped> GetMappedFeatures(List<FeatureRecord> features)
    {
        return features.Select(r => new FeatureRecordMapped
        {
            Timestamp = r.Timestamp,
            BestBid = (float)r.BestBid,
            BestAsk = (float)r.BestAsk,
            Spread = (float)r.Spread,
            Imbalance = float.IsNaN((float)r.Imbalance) ? 0 : (float)r.Imbalance,
            VWAPBid = (float)r.VWAPBid,
            VWAPAsk = (float)r.VWAPAsk,
            TotalTradeVolume = (float)r.TotalTradeVolume,
            TradeCount = (float)r.TradeCount,
            BuyTradeVolume = (float)r.BuyTradeVolume,
            SellTradeVolume = (float)r.SellTradeVolume,
            TradePriceChange = (float)r.TradePriceChange,
            MidPrice = (float)r.MidPrice,
            SpreadPct = (float)r.SpreadPct,
            PriceChangePct = (float)r.PriceChangePct,
            TradeIntensity = (float)r.TradeIntensity,
            Label = r.Label > 0.5f // если Label > 0.5, то true, иначе false
        }).ToList();
    }

    // Метод для oversampling: дублируем позитивные примеры так, чтобы их число приблизилось к числу негативных
    public List<FeatureRecordMapped> BalanceDataByOversampling(List<FeatureRecordMapped> data)
    {
        // Разбиваем данные на позитивные и негативные примеры
        var positives = data.Where(x => x.Label == true).ToList();
        var negatives = data.Where(x => x.Label == false).ToList();

        var positiveCount = positives.Count;
        var negativeCount = negatives.Count;

        // Если позитивных примеров нет, возвращаем исходный набор
        if (positiveCount == 0)
            return data;

        // Рассчитываем, во сколько раз нужно увеличить количество позитивных примеров
        var factor = negativeCount / positiveCount;
        var remainder = negativeCount - (factor * positiveCount);

        var oversampledPositives = new List<FeatureRecordMapped>();

        // Полностью дублируем позитивы factor раз
        for (var i = 0; i < factor; i++)
        {
            oversampledPositives.AddRange(positives);
        }

        // Добавляем ещё случайно выбранные позитивные примеры для достижения нужного числа
        var rand = new Random();
        oversampledPositives.AddRange(positives.OrderBy(x => rand.Next()).Take(remainder));

        // Объединяем негативные и oversampled позитивные примеры и перемешиваем
        List<FeatureRecordMapped> balanced = new List<FeatureRecordMapped>();
        balanced.AddRange(negatives);
        balanced.AddRange(oversampledPositives);

        return balanced.OrderBy(x => rand.Next()).ToList();
    }

// Метод для undersampling: выбираем случайную выборку негативных примеров так, чтобы их число было равно числу позитивных
    public List<FeatureRecordMapped> BalanceDataByUndersampling(List<FeatureRecordMapped> data)
    {
        var positives = data.Where(x => x.Label == true).ToList();
        var negatives = data.Where(x => x.Label == false).ToList();

        // Если позитивных примеров нет, возвращаем исходный набор
        if (!positives.Any())
            return data;

        var rand = new Random();
        // Случайным образом отбираем столько негативных, сколько позитивных
        negatives = negatives.OrderBy(x => rand.Next()).Take(positives.Count).ToList();

        var balanced = new List<FeatureRecordMapped>();
        balanced.AddRange(positives);
        balanced.AddRange(negatives);

        return balanced.OrderBy(x => rand.Next()).ToList();
    }

    /// <summary>
    /// Обучает модель на данных, загруженных из PostgreSQL, и выводит метрики.
    /// </summary>
    public (ITransformer, CalibratedBinaryClassificationMetrics) TrainModel()
    {
        // Загружаем данные из БД
        var features = LoadFeaturesFromPostgreSQL("features");
        // Преобразуем данные в тип, где все числовые признаки имеют тип float
        var mappedFeatures = GetMappedFeatures(features);

        var balancedFeatures = BalanceDataByOversampling(mappedFeatures);
        // Создаём набор с весами
        var weightedFeatures = GetWeightedFeatures(balancedFeatures);

        // Загружаем данные в IDataView
        var dataView = _mlContext.Data.LoadFromEnumerable(weightedFeatures);

        // Разбиваем данные на обучающую и тестовую выборки (например, 80/20)
        var split = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2);
        var trainData = split.TrainSet;
        var testData = split.TestSet;

        // Создаём пайплайн: объединяем признаки в один вектор
        var pipeline = _mlContext.Transforms.Concatenate("Features",
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
            // Обучаем модель с использованием LightGBM и задаём имя столбца весов ("Weight")
            .Append(_mlContext.BinaryClassification.Trainers.LightGbm(
                labelColumnName: "Label",
                featureColumnName: "Features",
                exampleWeightColumnName: "Weight",
                numberOfIterations: 100));
        // Обучаем модель
        var model = pipeline.Fit(trainData);

        // Оцениваем модель на тестовой выборке
        var predictions = model.Transform(testData);
        var metrics = _mlContext.BinaryClassification.Evaluate(predictions, labelColumnName: "Label");

        return (model, metrics);
    }

    public void EvaluateModelFromDB(string modelPath)
    {
        // Загружаем независимые данные из БД (например, данные за другой период)
        var features = LoadFeaturesFromPostgreSQL("features");

        // Преобразуем данные в формат для обучения (все числовые признаки в float, Label -> bool)
        var mappedFeatures = GetMappedFeatures(features);

        // Преобразуем список в IDataView
        var testData = _mlContext.Data.LoadFromEnumerable(mappedFeatures);

        // Загружаем модель из файла
        var loadedModel = _mlContext.Model.Load(modelPath, out var modelInputSchema);

        // Применяем модель к тестовым данным
        var predictions = loadedModel.Transform(testData);

        // Оцениваем модель (Label должен быть булевым)
        var metrics = _mlContext.BinaryClassification.Evaluate(predictions, labelColumnName: "Label");

        _logger.Information("Evaluation on independent data:");
        _logger.Information("Accuracy: {Accuracy}", metrics.Accuracy.ToString("P2"));
        _logger.Information("AUC: {AUC}", metrics.AreaUnderRocCurve.ToString("P2"));
        _logger.Information("F1 Score: {F1Score}", metrics.F1Score.ToString("P2"));
    }

    public (double Precision, double Recall, double F1, double Accuracy) EvaluateModelWithAdjustedThreshold(string modelPath, float newThreshold)
    {
        // Загружаем независимый набор данных из БД
        var features = LoadFeaturesFromPostgreSQL("features_ng_old");
        List<FeatureRecordMapped> mappedFeatures = GetMappedFeatures(features);
        var testData = _mlContext.Data.LoadFromEnumerable(mappedFeatures);

        // Загружаем модель из файла
        var loadedModel = _mlContext.Model.Load(modelPath, out var modelInputSchema);

        // Применяем модель к тестовым данным
        var predictions = loadedModel.Transform(testData);

        // Конвертируем результаты в список PredictionResult
        var predictionResults = _mlContext.Data.CreateEnumerable<PredictionResult>(predictions, reuseRowObject: false)
            .ToList();

        // Применяем новый порог: если Score >= newThreshold, считаем класс положительным
        foreach (var result in predictionResults)
        {
            result.PredictedLabel = result.Score >= newThreshold;
        }

        // Вычисляем значения confusion matrix
        var truePositive = predictionResults.Count(x => x.Label == true && x.PredictedLabel == true);
        var falsePositive = predictionResults.Count(x => x.Label == false && x.PredictedLabel == true);
        var trueNegative = predictionResults.Count(x => x.Label == false && x.PredictedLabel == false);
        var falseNegative = predictionResults.Count(x => x.Label == true && x.PredictedLabel == false);

        // Рассчитываем метрики
        var precision = (truePositive + falsePositive) > 0
            ? (double)truePositive / (truePositive + falsePositive)
            : 0;
        var recall = (truePositive + falseNegative) > 0
            ? (double)truePositive / (truePositive + falseNegative)
            : 0;
        var f1 = (precision + recall) > 0
            ? 2 * precision * recall / (precision + recall)
            : 0;
        var accuracy = (double)(truePositive + trueNegative) /
                       (truePositive + falsePositive + trueNegative + falseNegative);

        _logger.Information("Evaluation with adjusted threshold {Threshold}:", newThreshold);
        _logger.Information("Confusion Matrix: TP={TP}, FP={FP}, TN={TN}, FN={FN}",
            truePositive,
            falsePositive,
            trueNegative,
            falseNegative);
        _logger.Information("Precision: {Precision:P2}", precision);
        _logger.Information("Recall: {Recall:P2}", recall);
        _logger.Information("F1 Score: {F1Score:P2}", f1);
        _logger.Information("Accuracy: {Accuracy:P2}", accuracy);

        return (precision, recall, f1, accuracy);
    }

    /// <summary>
    /// Переразмечает фичи, расширяя позитивную разметку вокруг точек разворота.
    /// Если для окна с индексом i обнаружен разворот, то окна с индексами от (i - windowOffset) до (i + windowOffset)
    /// получают метку 1 (если они существуют).
    /// </summary>
    /// <param name="features">Список исходных фичей с первоначальной разметкой</param>
    /// <param name="thresholdPercent">Порог для определения разворота</param>
    /// <param name="windowOffset">Количество окон до и после точки разворота, которые следует отметить как позитивные</param>
    public void ReLabelFeatures(List<FeatureRecord> features, double thresholdPercent, int windowOffset)
    {
        // Вычисляем массив цен как среднее между BestBid и BestAsk
        double[] prices = features.Select(r => (r.BestBid + r.BestAsk) / 2).ToArray();

        // Получаем индексы точек разворота по существующему алгоритму
        var reversalIndices = FindTrendReversalIndices(prices, thresholdPercent);

        // Сбрасываем текущие метки (присваиваем 0 всем)
        foreach (var feature in features)
        {
            feature.Label = 0;
        }

        // Для каждого обнаруженного разворота помечаем окна вокруг него как позитивные
        foreach (int idx in reversalIndices)
        {
            int start = Math.Max(0, idx - windowOffset);
            int end = Math.Min(features.Count - 1, idx + windowOffset);
            for (int i = start; i <= end; i++)
            {
                features[i].Label = 1;
            }
        }

        // Дополнительно можно вывести информацию для отладки
        Console.WriteLine(
            $"Найдено {reversalIndices.Count} точек разворота. Позитивная разметка расширена на {windowOffset} окон до и после.");
    }

    /// <summary>
    /// Сохраняет обученную модель в файл.
    /// </summary>
    public void SaveModel(ITransformer model, string modelPath)
    {
        // Для сохранения модели желательно передать входную схему,
        // здесь используем данные из одной из выборок
        var sampleData = _mlContext.Data.LoadFromEnumerable(new List<FeatureRecord>());
        _mlContext.Model.Save(model, sampleData.Schema, modelPath);
        _logger.Information("Model saved to {Path}", modelPath);
    }

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
}
