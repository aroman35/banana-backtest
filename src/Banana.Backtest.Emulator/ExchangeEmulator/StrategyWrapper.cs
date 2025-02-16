using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy;
using CsvHelper;
using CsvHelper.Configuration;
using MathNet.Numerics.Statistics;
using Microsoft.ML;
using ScottPlot;
using Serilog;

namespace Banana.Backtest.Emulator.ExchangeEmulator;

// Vector(1 - N) Label = Lim(k/N)
// Vector(1) Label = 0.7
// Vector(2) Label = 0.8
// Vector(3) Label = 0.9
// Vector(4) Label = 1.0
public class StrategyWrapper : IStrategy
{
    private const int DATA_DEPTH = 128;
    private readonly ILogger _logger;
    private readonly TimeOnly _openTime = new(10, 0, 0);
    private readonly TimeOnly _closeTime = new(18, 50, 0);
    private readonly List<UserOrder> _userOrders = new();
    private readonly List<UserExecution> _userExecutions = new();
    private readonly Emulator _emulator;
    private readonly long _tradeDateOpenTimestamp;
    private readonly long _tradeDateCloseTimestamp;
    private readonly long _simulationStartedTimestamp;
    private readonly List<OrderBookSnapshot> _allOrderBooks = new();
    private readonly List<MarketDataItem<TradeUpdate>> _allTrades = new();
    private readonly List<FeaturesClass> _features = new();
    private readonly FeatureBuilder _featureBuilder;
    private OrderBookSnapshot _orderBook;
    private OrderBookDataClass _orderBookDataClass = new(25);

    private double _volumeExecuted;
    private double _volumeStd;
    private double _volumeEwma;

    private TradeUpdate[] _trades = new TradeUpdate[DATA_DEPTH];
    private double _lastPx;
    private int _tradesIdx;

    public StrategyWrapper(Emulator emulator, ILogger logger)
    {
        _emulator = emulator;
        _logger = logger.ForContext<StrategyWrapper>();
        var tradeDateOpenDateTime = new DateTime(_emulator.Hash.Date, _openTime, DateTimeKind.Local);
        var tradeDateCloseDateTime = new DateTime(_emulator.Hash.Date, _closeTime, DateTimeKind.Local);
        _tradeDateOpenTimestamp = tradeDateOpenDateTime.ToUnixTimeMilliseconds();
        _tradeDateCloseTimestamp = tradeDateCloseDateTime.ToUnixTimeMilliseconds();
        _simulationStartedTimestamp = Stopwatch.GetTimestamp();
        _featureBuilder = new FeatureBuilder(logger);
    }

    public void OrderBookUpdated(MarketDataItem<OrderBookSnapshot> orderBookSnapshot)
    {
        _featureBuilder.OrderBookUpdated(orderBookSnapshot);
        var features = _orderBookDataClass.Generate(orderBookSnapshot.Item);
        features.FeaturesTimestamp = orderBookSnapshot.DateTime;
        _features.AddRange(features);
    }

    public unsafe void AnonymousTradeReceived(MarketDataItem<TradeUpdate> trade)
    {
        _featureBuilder.AnonymousTradeReceived(trade);
        _allTrades.Add(trade);
    }

    public void UserExecutionReceived(UserExecution userExecution)
    {
        _userExecutions.Add(userExecution);
    }

    public void PlaceOrder(UserOrder order)
    {
        _userOrders.Add(order);
        _emulator.ProcessUserOrder(order);
    }

    public void SimulationFinished()
    {
        // BuildVerificationData();
        TrainModel();
        return;
        var remainedLimit = _userExecutions
            .Sum(execution => execution.ExecutedQuantity * (int)execution.Side);

        var value = remainedLimit * _lastPx;

        var total = _userExecutions
            .Sum(execution => execution.ExecutionPrice * execution.ExecutedQuantity * (int)execution.Side);

        var executedVolume = _userExecutions.Sum(execution => execution.ExecutionPrice * execution.ExecutedQuantity);
        var dirtyPnl = value - total;
        var brokerFee = executedVolume * 0.00025;
        var cleanPnl = dirtyPnl - brokerFee;
        var elapsedTime = Stopwatch.GetElapsedTime(_simulationStartedTimestamp);

        _logger.Information(
            "Trade date: {TradeDate} Strategy finished. Total trades: {TradesCount}, PnL: {Pnl} (Broker Fee: {Fee} | Volume: {Volume}) Duration: {Duration}",
            _emulator.Hash.Date,
            _userExecutions.Count,
            cleanPnl,
            brokerFee,
            executedVolume,
            elapsedTime);
    }

    private void TrainModel()
    {
        _featureBuilder.FlushRemaining();
        _featureBuilder.LabelFeatures(0.005);
        _featureBuilder.RemoveEmptyFeatures();
        _featureBuilder.SaveFeaturesToPostgreSql(Environment.GetEnvironmentVariable("PG_CONNECTION_STRING"));
        _logger.Information("Features were built and saved to DB for {Hash}", _emulator.Hash);
        return;
        var labeledCount = _featureBuilder.Features.Count(x => x.Label > 0);
        if (_allTrades.Count == 0)
            return;
        var pricesMovingAvg = Statistics.MovingAverage(_allTrades.Select(x => x.Item.Price), 50).ToArray();
        var trendChangedIndexes = FindTrendReversalIndices(pricesMovingAvg, 0.001);
        LabelFeatures(trendChangedIndexes);
        SaveChart(trendChangedIndexes, pricesMovingAvg);
        _logger.Information("Preparing data for model");
        return;
        var mlContext = new MLContext(seed: 0);
        var model = Train(mlContext, _features.Select(MlDataInputClass.Create));
        EvaluateModel(mlContext, model);

        // using (var outputModelFileStream = File.Create("D:/models/regression.onnx"))
        // {
        //     mlContext.Model.ConvertToOnnx(model, dataView, outputModelFileStream);
        // }
        _logger.Information("Model trained");
    }

    private static List<int> FindTrendReversalIndices(double[] prices, double thresholdPercent)
    {
        List<int> reversalIndices = new List<int>();

        // Если данных недостаточно – возвращаем пустой список
        if (prices == null || prices.Length < 2)
            return reversalIndices;

        // Инициализация: первая цена считается исходной точкой
        int lastExtremeIndex = 0;
        double lastExtremePrice = prices[0];
        reversalIndices.Add(lastExtremeIndex);

        // currentTrend: 0 - неопределён, 1 - восходящий, -1 - нисходящий
        int currentTrend = 0;

        // Проходим по массиву начиная со второй цены
        for (int i = 1; i < prices.Length; i++)
        {
            double price = prices[i];

            if (currentTrend == 0)
            {
                // Определяем начальный тренд, если цена изменилась достаточно
                if (price >= lastExtremePrice * (1 + thresholdPercent))
                {
                    currentTrend = 1; // восходящий тренд
                    lastExtremeIndex = i;
                    lastExtremePrice = price;
                    reversalIndices.Add(i);
                }
                else if (price <= lastExtremePrice * (1 - thresholdPercent))
                {
                    currentTrend = -1; // нисходящий тренд
                    lastExtremeIndex = i;
                    lastExtremePrice = price;
                    reversalIndices.Add(i);
                }
            }
            // если тренд восходящий
            else if (currentTrend == 1)
            {
                if (price > lastExtremePrice)
                {
                    // Цена продолжает расти — обновляем максимум
                    lastExtremeIndex = i;
                    lastExtremePrice = price;
                    // Обновляем последний зафиксированный экстремум (точку разворота)
                    reversalIndices[reversalIndices.Count - 1] = i;
                }
                else if (price <= lastExtremePrice * (1 - thresholdPercent))
                {
                    // Если цена упала более чем на thresholdPercent от максимума,
                    // регистрируем разворот: восходящий -> нисходящий
                    currentTrend = -1;
                    lastExtremeIndex = i;
                    lastExtremePrice = price;
                    reversalIndices.Add(i);
                }
            }
            // если тренд нисходящий
            else if (currentTrend == -1)
            {
                if (price < lastExtremePrice)
                {
                    // Цена продолжает снижаться — обновляем минимум
                    lastExtremeIndex = i;
                    lastExtremePrice = price;
                    reversalIndices[reversalIndices.Count - 1] = i;
                }
                else if (price >= lastExtremePrice * (1 + thresholdPercent))
                {
                    // Если цена выросла более чем на thresholdPercent от минимума,
                    // регистрируем разворот: нисходящий -> восходящий
                    currentTrend = 1;
                    lastExtremeIndex = i;
                    lastExtremePrice = price;
                    reversalIndices.Add(i);
                }
            }
        }

        return reversalIndices;
    }

    private void LabelFeatures(List<int> trendChangedIndexes)
    {
        FeaturesClass nextFeature = default;
        using var enumerator = _features.GetEnumerator();
        if (enumerator.MoveNext())
            nextFeature = enumerator.Current;
        else
        {
            _logger.Warning("No features are stored for {Hash}", _emulator.Hash);
            return;
        }
        foreach (var index in trendChangedIndexes)
        {
            var trade = _allTrades[index];
            var pointTimestamp = trade.Timestamp.AsDateTime();
            var features = new List<FeaturesClass>();
            features.Add(nextFeature);
            while (nextFeature.FeaturesTimestamp <= pointTimestamp && enumerator.MoveNext())
            {
                features.Add(nextFeature);
                nextFeature = enumerator.Current;
            }

            for (var i = 0; i < features.Count; i++)
            {
                features[i].Label = (float)(1.0 / features.Count * i);
            }
        }
    }

    private void SaveChart(List<int> trendChangedIndexes, double[] pricesMovingAvg)
    {
        var multiPlot = new MultiPlot(5120, 1440, 2);

// Верхний график (индекс 0)
        var topPlot = multiPlot.GetSubplot(0, 0);

// Нижний график (индекс 1)
        var bottomPlot = multiPlot.GetSubplot(1, 0);

// --- Верхний график: Prices и Points ---
        var trendPoints = trendChangedIndexes
            .Select(x => _allTrades[x].Timestamp.AsDateTime().ToOADate())
            .ToArray();
        var trendPrices = trendChangedIndexes
            .Select(x => pricesMovingAvg[x])
            .ToArray();
        var times = _allTrades
            .Select(x => x.Timestamp.AsDateTime().ToOADate())
            .ToArray();

        var pricesScatter = topPlot.AddScatter(times, pricesMovingAvg, label: "Prices");
        pricesScatter.LineWidth = 2;

        var pointsScatter = topPlot.AddScatter(trendPoints, trendPrices, label: "Points");
        pointsScatter.MarkerSize = 10;
        pointsScatter.MarkerShape = MarkerShape.filledCircle;

        topPlot.XAxis.DateTimeFormat(true);
        topPlot.Title("Prices & Trend Points");

// --- Нижний график: Features ---
        var featuresTimes = _features
            .Select(x => x.FeaturesTimestamp.ToOADate())
            .ToArray();
        var featuresLabels = _features
            .Select(x => (double)x.Label)
            .ToArray();

        var featuresScatter = bottomPlot.AddScatter(featuresTimes, featuresLabels, label: "Features");
        featuresScatter.LineWidth = 2;
        featuresScatter.Color = Color.Coral;

        bottomPlot.XAxis.DateTimeFormat(true);
        bottomPlot.YAxis.Label("Features Axis");
        bottomPlot.Title("Features");

// Чтобы оси X совпадали, можно принудительно задать одинаковые пределы
// (если данные не меняются динамически, можно вычислить общие границы)
        double commonXMin = times.Min();
        double commonXMax = times.Max();
        topPlot.SetAxisLimits(xMin: commonXMin, xMax: commonXMax);
        bottomPlot.SetAxisLimits(xMin: commonXMin, xMax: commonXMax);

        var filename = $"D:\\models\\chart_{_emulator.Hash.Symbol}_{_emulator.Hash.Date:dd.MM.yyyy}.jpg";
        multiPlot.SaveFig(filename);
    }

    private ITransformer Train(MLContext mlContext, IEnumerable<MlDataInputClass> data)
    {
        var dataView = mlContext.Data.LoadFromEnumerable(data);
        var pipeline = mlContext.Transforms
            .CopyColumns(outputColumnName: "Label", inputColumnName: "Rate5Sec")
            .Append(mlContext.Transforms.Concatenate("Features",
                "Bid1PriceMA",
                "Bid2PriceMA",
                "Bid3PriceMA",
                "Bid4PriceMA",
                "Bid1VolumeMA",
                "Bid2VolumeMA",
                "Bid3VolumeMA",
                "Bid4VolumeMA",
                "Ask1PriceMA",
                "Ask2PriceMA",
                "Ask3PriceMA",
                "Ask4PriceMA",
                "Ask1VolumeMA",
                "Ask2VolumeMA",
                "Ask3VolumeMA",
                "Ask4VolumeMA",
                "PeriodMs"))
            .Append(mlContext.Regression.Trainers.FastTree());
        _logger.Information("Training model");
        var model = pipeline.Fit(dataView);
        return model;
    }

    private void EvaluateModel(MLContext mlContext, ITransformer model)
    {
        var dataView = mlContext.Data.LoadFromTextFile<MlDataInputClass>("D:/models/test_data.csv", hasHeader: true, separatorChar: ',');
        var predictions = model.Transform(dataView);
        var metrics = mlContext.Regression.Evaluate(predictions, "Label", "Score");
        _logger.Information("Metrics evaluated");
        _logger.Information("RSquared Score: {RSquared}", metrics.RSquared);
        _logger.Information("RootMeanSquaredError: {RootMeanSquaredError}", metrics.RootMeanSquaredError);
    }

    private void TestSinglePrediction(MLContext mlContext, ITransformer model)
    {
        var predictionFunction = mlContext.Model.CreatePredictionEngine<MlDataInputClass, MlDataOutputClass>(model);
    }

    private static unsafe double StandardDeviation(double* dataPtr, int size)
    {
        if (size == 0)
            throw new ArgumentException("Data cannot be empty.");

        double sum = 0;
        double sumSquared = 0;

        // Calculate the sum of the values
        for (var i = 0; i < size; i++)
        {
            sum += dataPtr[i];
            sumSquared += dataPtr[i] * dataPtr[i];
        }

        var mean = sum / size;
        var variance = sumSquared / size - mean * mean;
        return Math.Sqrt(variance);
    }

    private static unsafe double Ewma(double* dataPtr, int size, double alpha)
    {
        if (size == 0)
            throw new ArgumentException("Data cannot be empty.");

        if (alpha is <= 0 or >= 1)
            throw new ArgumentException("Alpha must be between 0 and 1 exclusive.");

        var ewma = dataPtr[0]; // Initial value
        for (var i = 1; i < size; i++)
        {
            ewma = alpha * dataPtr[i] + (1 - alpha) * ewma;
        }

        return ewma;
    }
}
