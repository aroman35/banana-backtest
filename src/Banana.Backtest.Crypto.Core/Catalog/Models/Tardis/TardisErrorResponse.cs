namespace Banana.Backtest.Crypto.Core.Catalog.Models.Tardis;

public class TardisErrorResponse
{
    public DatasetInfo? DatasetInfo { get; set; }

    public bool IsTradingDate(DateOnly date)
    {
        return DatasetInfo is not null &&
               date > DateOnly.FromDateTime(DatasetInfo.AvailableSince) &&
               date < DateOnly.FromDateTime(DatasetInfo.AvailableTo);
    }
}
