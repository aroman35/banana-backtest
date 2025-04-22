using Microsoft.ML;

namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy.Runtime;

public class ModelHandler(MLContext mlContext)
{
    private readonly string _modelPath = "D:/models/model_regression.zip";

    /// <summary>
    /// Saves the provided ML.NET model to a file.
    /// </summary>
    /// <param name="model">Trained model.</param>
    /// <param name="schema">Schema of the training data.</param>
    public void SaveModel(ITransformer model, DataViewSchema schema)
    {
        mlContext.Model.Save(model, schema, _modelPath);
    }

    /// <summary>
    /// Loads the ML.NET model from a file.
    /// </summary>
    /// <returns>Loaded model.</returns>
    public ITransformer LoadModel()
    {
        if (!File.Exists(_modelPath))
        {
            throw new FileNotFoundException($"Model file not found: {_modelPath}");
        }

        ITransformer model = mlContext.Model.Load(_modelPath, out DataViewSchema schema);
        return model;
    }

    /// <summary>
    /// Creates a prediction engine from the loaded model and returns a prediction for the given input.
    /// </summary>
    /// <param name="input">New data for prediction.</param>
    /// <returns>Prediction output.</returns>
    public ModelOutput Predict(ModelInput input)
    {
        ITransformer loadedModel = LoadModel();
        var predEngine = mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(loadedModel);
        ModelOutput prediction = predEngine.Predict(input);
        return prediction;
    }
}
