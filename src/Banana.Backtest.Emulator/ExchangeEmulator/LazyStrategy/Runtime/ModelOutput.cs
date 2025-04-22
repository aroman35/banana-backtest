using Microsoft.ML.Data;

namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy.Runtime;

// Define the output schema (score is the predicted label)
public class ModelOutput
{
    [ColumnName("Score")]
    public float Score { get; set; }
}
