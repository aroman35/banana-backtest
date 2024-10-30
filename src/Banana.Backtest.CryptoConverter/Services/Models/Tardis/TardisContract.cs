using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.CryptoConverter.Extensions;

namespace Banana.Backtest.CryptoConverter.Services.Models.Tardis;

public class TardisContract
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

    public InstrumentInfo ToInstrumentInfo()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(BaseCurrency);
        ArgumentException.ThrowIfNullOrWhiteSpace(QuoteCurrency);
        ArgumentException.ThrowIfNullOrWhiteSpace(Exchange);

        var symbol = Symbol.Parse(BaseCurrency, QuoteCurrency, Exchange.GetEnumByDescription<Exchange>());
        return new InstrumentInfo
        {
            DatasetId = DatasetId,
            Symbol = symbol,
            IsActive = Active,
            AvailableSince = DateOnly.FromDateTime(AvailableSince),
        };
    }
}