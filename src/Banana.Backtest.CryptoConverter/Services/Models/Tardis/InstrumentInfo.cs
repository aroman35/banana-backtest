using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.CryptoConverter.Services.Models.Tardis;

public class InstrumentInfo
{
    public string? Id { get; init; }
    public string? DatasetId { get; init; }
    public string? Exchange { get; init; }
    public string? BaseCurrency { get; init; }
    public string? QuoteCurrency { get; init; }
    public string? Type { get; init; }
    public Symbol Symbol { get; init; }
    public bool IsActive { get; init; }
    public DateOnly AvailableSince { get; init; }
    public decimal PriceIncrement { get; init; }
    public decimal AmountIncrement { get; init; }
    public decimal MinTradeAmount { get; init; }
    public decimal MakerFee { get; init; }
    public decimal TakerFee { get; init; }
    public bool Margin { get; init; } // for spot
    public bool Inverse { get; init; } // for futures
    public string? ContractType { get; init; } // linear, inverse
    public decimal? ContractMultiplier { get; init; }
    public string? UnderlyingIndex { get; init; }
    public DateOnly Listing { get; init; }
}
