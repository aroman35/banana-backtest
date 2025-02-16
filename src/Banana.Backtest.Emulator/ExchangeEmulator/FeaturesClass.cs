using Microsoft.ML.Data;

namespace Banana.Backtest.Emulator.ExchangeEmulator;

public class FeaturesClass
{
    [LoadColumn(0)]
    public float Bid1PriceMA { get; set; }

    [LoadColumn(1)]
    public float Bid2PriceMA { get; set; }

    [LoadColumn(2)]
    public float Bid3PriceMA { get; set; }

    [LoadColumn(3)]
    public float Bid4PriceMA { get; set; }

    [LoadColumn(4)]
    public float Bid1VolumeMA { get; set; }

    [LoadColumn(5)]
    public float Bid2VolumeMA { get; set; }

    [LoadColumn(6)]
    public float Bid3VolumeMA { get; set; }

    [LoadColumn(7)]
    public float Bid4VolumeMA { get; set; }

    [LoadColumn(8)]
    public float Ask1PriceMA { get; set; }

    [LoadColumn(9)]
    public float Ask2PriceMA { get; set; }

    [LoadColumn(10)]
    public float Ask3PriceMA { get; set; }

    [LoadColumn(11)]
    public float Ask4PriceMA { get; set; }

    [LoadColumn(12)]
    public float Ask1VolumeMA { get; set; }

    [LoadColumn(13)]
    public float Ask2VolumeMA { get; set; }

    [LoadColumn(14)]
    public float Ask3VolumeMA { get; set; }

    [LoadColumn(15)]
    public float Ask4VolumeMA { get; set; }

    [LoadColumn(16)]
    public float PeriodMs { get; set; }

    [LoadColumn(17)]
    public DateTime FeaturesTimestamp { get; set; }

    [LoadColumn(18)]
    public float Label { get; set; }
}
