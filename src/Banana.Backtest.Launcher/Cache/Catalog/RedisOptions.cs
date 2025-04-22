using StackExchange.Redis;

namespace Banana.Backtest.Launcher.Cache.Catalog;

public class RedisOptions
{
    public required string ConnectionString { get; init; }

    public required string Password { get; init; }

    public required string KeyPrefix { get; init; }

    public required string ServiceName { get; init; }

    public bool AbortOnConnectionFail { get; init; }
    public int ConnectTimeout { get; init; }

    public int CatalogDatabase { get; set; }

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
