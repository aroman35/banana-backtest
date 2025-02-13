namespace Banana.Backtest.Emulator.ExchangeEmulator;

public class FeaturesClass
{
    public double Bid1PriceMA { get; set; }
    public double Bid2PriceMA { get; set; }
    public double Bid3PriceMA { get; set; }
    public double Bid4PriceMA { get; set; }

    public double Bid1VolumeMA { get; set; }
    public double Bid2VolumeMA { get; set; }
    public double Bid3VolumeMA { get; set; }
    public double Bid4VolumeMA { get; set; }

    public double Ask1PriceMA { get; set; }
    public double Ask2PriceMA { get; set; }
    public double Ask3PriceMA { get; set; }
    public double Ask4PriceMA { get; set; }

    public double Ask1VolumeMA { get; set; }
    public double Ask2VolumeMA { get; set; }
    public double Ask3VolumeMA { get; set; }
    public double Ask4VolumeMA { get; set; }

    public double PeriodMs { get; set; }

    public DateTime FeaturesTimestamp { get; set; }
    public double Rate5Sec { get; set; }
    public double Next5SecPrice { get; set; }
    public double MidPrice { get; set; }
}
