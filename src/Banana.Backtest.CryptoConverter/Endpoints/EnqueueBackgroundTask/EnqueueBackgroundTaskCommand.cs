using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.CryptoConverter.Endpoints.EnqueueBackgroundTask;

/// <summary>
/// Schedule direct downloading and convert for the given exchange
/// </summary>
/// <param name="Exchange">Exchange</param>
/// <param name="StartDate">Start date including</param>
/// <param name="EndDate">End date including</param>
/// <param name="Shift">Optional: delay</param>
public record EnqueueBackgroundTaskCommand(Exchange Exchange, DateOnly StartDate, DateOnly EndDate, TimeSpan? Shift);
