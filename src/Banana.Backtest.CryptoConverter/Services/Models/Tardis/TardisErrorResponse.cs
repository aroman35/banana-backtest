namespace Banana.Backtest.CryptoConverter.Services.Models.Tardis;

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

public class DatasetInfo
{
    public DateTime AvailableSince { get; set; }
    public DateTime AvailableTo { get; set; }
}