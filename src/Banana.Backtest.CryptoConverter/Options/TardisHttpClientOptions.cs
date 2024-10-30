using Microsoft.Extensions.Http.Resilience;

namespace Banana.Backtest.CryptoConverter.Options;

public class TardisHttpClientOptions
{
    public string? ApiUrl { get; set; }
    public string? DatasetsUrl { get; set; }
    public string? AccessToken { get; set; }
    public TimeSpan HandlerLifetime { get; set; }
    public HttpRateLimiterStrategyOptions? RateLimiter { get; set; }
    public HttpRetryStrategyOptions? Retry { get; set; }
    public HttpTimeoutStrategyOptions? AttemptTimeout { get; set; }
    public HttpTimeoutStrategyOptions? TotalRequestTimeout { get; set; }
    public HttpCircuitBreakerStrategyOptions? CircuitBreaker { get; set; }
}