using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.CryptoConverter.Endpoints.EnqueueBackgroundTask;

public record EnqueueBackgroundTaskCommand(Exchange Exchange, DateOnly StartDate, DateOnly EndDate, TimeSpan? Shift);