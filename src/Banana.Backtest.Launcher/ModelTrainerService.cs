using Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy;

namespace Banana.Backtest.Launcher;

public class ModelTrainerService(ILogger logger) : IHostedService
{
    private readonly ILogger _logger = logger.ForContext<ModelTrainerService>();
    private readonly string _connectionString = Environment.GetEnvironmentVariable("PG_CONNECTION_STRING");

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var trainer = new ModelTrainer(_connectionString, "labeled_features", 0.005, 1.0, 1, logger);
        // Обучаем модель на данных из БД
        var (model, metrics) = trainer.TrainModel();
        trainer.SaveFeaturesToPostgreSQL("NG");
        _logger.Information("Model trained done, thresholdPercent {TP}, windowOffset: {WO}", 0.005, 1.0);
        _logger.Information("Regression model metrics:");
        _logger.Information("R^2: {RSquared:P2}", metrics.RSquared);
        _logger.Information("RMSE: {RMSE:F4}", metrics.RootMeanSquaredError);
        _logger.Information("MAE: {MAE:F4}", metrics.MeanAbsoluteError);

        // var metricsDataList = new List<MetricsData>();
        // for (var threshold = 0.0005; threshold <= 0.005; threshold += 0.0001)
        // {
        //     for (var windowOffset = 1; windowOffset < 5; windowOffset++)
        //     {
        //         var trainer = new ModelTrainer(_connectionString, threshold, windowOffset, logger);
        //         // Обучаем модель на данных из БД
        //         var (model, metrics) = trainer.TrainModel();
        //         _logger.Information("Model trained done, thresholdPercent {TP}, windowOffset: {WO}", threshold, windowOffset);
        //         _logger.Information("Accuracy: {Accuracy}", metrics.Accuracy.ToString("P2"));
        //         _logger.Information("AUC: {Auc}", metrics.AreaUnderRocCurve.ToString("P2"));
        //         _logger.Information("F1 Score: {Score}", metrics.F1Score.ToString("P2"));
        //         var metricsData = new MetricsData(threshold, windowOffset, metrics.Accuracy, metrics.AreaUnderRocCurve, metrics.F1Score);
        //         metricsDataList.Add(metricsData);
        //         // Сохраняем модель в файл
        //         // trainer.SaveModel(model, "D:/models/model.zip");
        //         // _logger.Information("Model saved to model.zip");
        //         //
        //         // for (var i = 0.1f; i < 0.2f; i += 0.025f)
        //         // {
        //         //     trainer.EvaluateModelWithAdjustedThreshold("D:/models/model.zip", i);
        //         // }
        //     }
        // }
        // File.WriteAllText("D:/models/scores.json", JsonConvert.SerializeObject(metricsDataList, Formatting.Indented));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public record MetricsData(
        double Threshold,
        int WindowOffset,
        double Accuracy,
        double AreaUnderRocCurve,
        double F1Score);
}
