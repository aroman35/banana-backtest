using System.Globalization;
using Banana.Backtest.CryptoConverter.Options;
using Banana.Backtest.CryptoConverter.Scheduler;
using Hangfire;
using Hangfire.Prometheus.NetCore;
using Hangfire.Redis.StackExchange;
using StackExchange.Redis;

namespace Banana.Backtest.CryptoConverter.Extensions;

public static class SchedulerExtensions
{
    public static void ConfigureHangfire(this IServiceCollection services, IConfiguration configuration)
    {
        var redisOptions = configuration.GetOptions<RedisOptions>();
        var converterOptions = configuration.GetOptions<ConverterOptions>();
        services.AddHangfire((provider, hangfire) =>
        {
            hangfire.UseRedisStorage(
                provider.GetRequiredService<IConnectionMultiplexer>(),
                new RedisStorageOptions
                {
                    Db = redisOptions.SchedulerDatabase,
                    Prefix = redisOptions.KeyPrefix,
                    UseTransactions = true,
                    DeletedListSize = 100_000,
                    SucceededListSize = 100_000
                });
            hangfire.UseSerilogLogProvider();
            hangfire.UseActivator(HangfireDefaults.JobActivator(provider));
            hangfire.UseSimpleAssemblyNameTypeSerializer();
            hangfire.UseDefaultCulture(CultureInfo.InvariantCulture, CultureInfo.InvariantCulture);
            hangfire.UseSerializerSettings(HangfireDefaults.JsonSerializerSettings);
        });
        services.AddHangfireServer(server =>
        {
            server.WorkerCount = converterOptions.WorkersCount;
            server.ServerName = converterOptions.ServerName;
            server.MaxDegreeOfParallelismForSchedulers = converterOptions.MaxDegreeOfParallelismForSchedulers;
            server.Queues = converterOptions.Queues;
        });
        services.AddSingleton<JobActivator, HangfireJobActivator>();
        services.AddHostedService<JobsScheduleInitializer>();
    }

    public static void UseHangfireDashboard(this IApplicationBuilder app)
    {
        app.UsePrometheusHangfireExporter();
        app.UseHangfireDashboard(options: new DashboardOptions
        {
            DarkModeEnabled = true,
            DisplayStorageConnectionString = true,
            DashboardTitle = "Banana Backtest Crypto Converter",
            DefaultRecordsPerPage = 50,
            Authorization = [EmptyDashboardAuthorizationFilter.Instance]
        });
    }
}