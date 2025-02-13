using Microsoft.ML.Data;

namespace Banana.Backtest.Emulator.ExchangeEmulator;

public class MlDataOutputClass
{
    [ColumnName("Score")]
    public float Rate5Sec { get; set; }
}
