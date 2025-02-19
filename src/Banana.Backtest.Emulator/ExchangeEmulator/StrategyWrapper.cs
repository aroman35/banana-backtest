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
        var featuresRepository = new FeatureRepository(
            Environment.GetEnvironmentVariable("PG_CONNECTION_STRING"),
            "labeled_features",
            "NG",
            logger);
        _featureBuilder = new FeatureBuilder(featuresRepository, logger);
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
        TrainModel();
    }

    private void TrainModel()
    {
        // var tradesRepository = new TradeRepository(Environment.GetEnvironmentVariable("PG_CONNECTION_STRING"), _logger);
        // tradesRepository.BulkInsertTrades(_allTrades, "trades", "NG");

        _featureBuilder.FlushRemaining();
        _featureBuilder.SaveFeaturesToPostgreSQL(Environment.GetEnvironmentVariable("PG_CONNECTION_STRING"), "labeled_features", "NG");
        _logger.Information("Features were built and saved to DB for {Hash}", _emulator.Hash);
    }
}
