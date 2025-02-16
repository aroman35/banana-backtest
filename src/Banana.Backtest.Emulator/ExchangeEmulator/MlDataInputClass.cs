using Microsoft.ML.Data;

namespace Banana.Backtest.Emulator.ExchangeEmulator;

public class MlDataInputClass
{
    public static MlDataInputClass Create(FeaturesClass features)
    {
        return new MlDataInputClass
        {
            Bid1PriceMA = (float)features.Bid1PriceMA,
            Bid2PriceMA = (float)features.Bid2PriceMA,
            Bid3PriceMA = (float)features.Bid3PriceMA,
            Bid4PriceMA = (float)features.Bid4PriceMA,

            Bid1VolumeMA = (float)features.Bid1VolumeMA,
            Bid2VolumeMA = (float)features.Bid2VolumeMA,
            Bid3VolumeMA = (float)features.Bid3VolumeMA,
            Bid4VolumeMA = (float)features.Bid4VolumeMA,

            Ask1PriceMA = (float)features.Ask1PriceMA,
            Ask2PriceMA = (float)features.Ask2PriceMA,
            Ask3PriceMA = (float)features.Ask3PriceMA,
            Ask4PriceMA = (float)features.Ask4PriceMA,

            Ask1VolumeMA = (float)features.Ask1VolumeMA,
            Ask2VolumeMA = (float)features.Ask2VolumeMA,
            Ask3VolumeMA = (float)features.Ask3VolumeMA,
            Ask4VolumeMA = (float)features.Ask4VolumeMA,

            PeriodMs = (float)features.PeriodMs,
            Label = (float)features.Label,
        };
    }

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
    public float Label { get; set; }
}
