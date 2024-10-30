using Banana.Backtest.CryptoConverter.Converters;
using Hangfire;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Banana.Backtest.CryptoConverter.Scheduler;

public static class HangfireDefaults
{
    public static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        Converters = new List<JsonConverter>
        {
            SymbolNewtonsoftJsonConverter.Instance,
            new StringEnumConverter()
        }
    };
    
    public const string DEFAULT_QUEUE = "default";
    public const string TRADES_QUEUE = "trades";
    public const string LEVEL_UPDATES_QUEUE = "levelupdates";
    public const string INSTRUMENTS_REFRESH_QUEUE = "instrumetsrefresh";
    public const string CONVERT_EXCHANGE_QUEUE = "convertexchange";

    public static JobActivator JobActivator(IServiceProvider provider) =>
        provider.GetRequiredService<JobActivator>();
}