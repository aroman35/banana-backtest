using StackExchange.Redis;

namespace Banana.Backtest.CryptoConverter.Options;

public class RedisOptions
{
    /// <summary>
    /// Redis ConnectionString
    /// </summary>
    public string ConnectionString { get; init; } = null!;

    /// <summary>
    /// Redis password
    /// </summary>

    public string Password { get; init; } = null!;

    /// <summary>
    /// Redis KeyPrefix
    /// </summary>
    public string KeyPrefix { get; init; } = null!;
    
    /// <summary>
    /// ServiceName in Sentinel
    /// </summary>
    public string ServiceName { get; init; } = null!;

    /// <summary>
    /// Reconnect using current multiplexer if the connection was lost
    /// </summary>
    public bool AbortOnConnectionFail { get; init; }

    /// <summary>
    /// Connect timeout
    /// </summary>
    public int ConnectTimeout { get; init; }

    public int SchedulerDatabase { get; set; }

    public int CatalogDatabase { get; set; }

    /// <summary>
    /// Create connection string with unique client name
    /// </summary>
    /// <returns></returns>
    public ConfigurationOptions ToConfigurationOptions()
    {
        var options = ConfigurationOptions.Parse(ConnectionString);
        options.Password = Password;
        options.AbortOnConnectFail = AbortOnConnectionFail;
        options.ConnectTimeout = ConnectTimeout;
        options.ReconnectRetryPolicy = new ExponentialRetry(10);

        options.ClientName = $"banana-converter-{Guid.NewGuid():N}";
        options.ServiceName = ServiceName;
        options.AllowAdmin = true;

        return options;
    }
}