using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.CryptoConverter.Services.Models.Tardis;

public class InstrumentInfo
{
    public string? DatasetId { get; init; }
    public Symbol Symbol { get; init; }
    public bool IsActive { get; init; }
    public DateOnly AvailableSince { get; init; }
}