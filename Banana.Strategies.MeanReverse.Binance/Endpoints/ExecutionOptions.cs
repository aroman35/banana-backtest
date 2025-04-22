using Binance.Net.Enums;

namespace Banana.Strategies.MeanReverse.Binance.Endpoints;

public class ExecutionOptions
{
    public TimeInForce LimitOrderDefaultTimeInForce { get; init; }
    public TimeSpan OrderDefaultTimeout { get; init; }
}
