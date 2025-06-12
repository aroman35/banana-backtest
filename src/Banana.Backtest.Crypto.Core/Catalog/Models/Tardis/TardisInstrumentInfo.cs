using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.Crypto.Core.Extensions;

namespace Banana.Backtest.Crypto.Core.Catalog.Models.Tardis;

public class TardisInstrumentInfo
{
    public string? Id { get; set; }
    public string? DatasetId { get; set; }
    public string? Exchange { get; set; }
    public string? BaseCurrency { get; set; }
    public string? QuoteCurrency { get; set; }
    public string? Type { get; set; } // spot, perpetual, futures
    public bool Active { get; set; }
    public DateTime AvailableSince { get; set; }
    public decimal PriceIncrement { get; set; }
    public decimal AmountIncrement { get; set; }
    public decimal MinTradeAmount { get; set; }
    public decimal MakerFee { get; set; }
    public decimal TakerFee { get; set; }
    public bool Margin { get; set; } // for spot
    public bool Inverse { get; set; } // for futures
    public string? ContractType { get; set; } // linear, inverse
    public decimal? ContractMultiplier { get; set; }
    public string? UnderlyingIndex { get; set; }
    public DateTime Listing { get; set; }

    public InstrumentInfo ToInstrumentInfo()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(BaseCurrency);
        ArgumentException.ThrowIfNullOrWhiteSpace(QuoteCurrency);
        ArgumentException.ThrowIfNullOrWhiteSpace(Exchange);

        var symbol = Symbol.Parse(BaseCurrency, QuoteCurrency, Exchange.GetEnumByDescription<Exchange>());
        return new InstrumentInfo
        {
            Id = Id,
            Exchange = Exchange,
            BaseCurrency = BaseCurrency,
            QuoteCurrency = QuoteCurrency,
            Type = Type,
            PriceIncrement = PriceIncrement,
            AmountIncrement = AmountIncrement,
            MinTradeAmount = MinTradeAmount,
            MakerFee = MakerFee,
            TakerFee = TakerFee,
            Margin = Margin,
            Inverse = Inverse,
            ContractType = ContractType,
            ContractMultiplier = ContractMultiplier,
            UnderlyingIndex = UnderlyingIndex,
            DatasetId = DatasetId,
            Symbol = symbol,
            IsActive = Active,
            AvailableSince = DateOnly.FromDateTime(AvailableSince),
            Listing = DateOnly.FromDateTime(Listing)
        };
    }
}
