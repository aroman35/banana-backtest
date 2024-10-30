using System.Net.Http.Headers;
using Banana.Backtest.CryptoConverter.Options;

namespace Banana.Backtest.CryptoConverter.Extensions;

public static class HttpClientExtensions
{
    public static void ConfigureHttpClient(this IHttpClientBuilder builder, TardisHttpClientOptions httpClientOptions)
    {
        ArgumentNullException.ThrowIfNull(httpClientOptions);
        builder
            .ConfigureHttpClient((_, client) =>
            {
                if (!string.IsNullOrWhiteSpace(httpClientOptions.AccessToken))
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", httpClientOptions.AccessToken);
            })
            .SetHandlerLifetime(httpClientOptions.HandlerLifetime)
            .AddStandardResilienceHandler(options =>
            {
                if (httpClientOptions.Retry is not null)
                    options.Retry = httpClientOptions.Retry;
                if (httpClientOptions.RateLimiter is not null)
                    options.RateLimiter = httpClientOptions.RateLimiter;
                if (httpClientOptions.AttemptTimeout is not null)
                    options.AttemptTimeout = httpClientOptions.AttemptTimeout;
                if (httpClientOptions.TotalRequestTimeout is not null)
                    options.TotalRequestTimeout = httpClientOptions.TotalRequestTimeout;
                if (httpClientOptions.CircuitBreaker is not null)
                    options.CircuitBreaker = httpClientOptions.CircuitBreaker;
            });
    }
}