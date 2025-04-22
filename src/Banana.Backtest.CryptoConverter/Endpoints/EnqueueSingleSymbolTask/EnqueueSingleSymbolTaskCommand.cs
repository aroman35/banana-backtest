using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.CryptoConverter.Endpoints.EnqueueSingleSymbolTask;

public record EnqueueSingleSymbolTaskCommand(DateOnly StartDate, DateOnly EndDate, Symbol Symbol);
