namespace Banana.Backtest.Launcher.Cache;

public class CacheOptions
{
    public required string StoragePath { get; init; }
    public required string CachePath { get; init; }
    public DateOnly EmulationStartDate { get; init; }
    public DateOnly EmulationEndDate { get; init; }
    public required string[] Assets { get; init; }
}
