using System.Globalization;
using Banana.Backtest.Crypto.Core.Options;
using Banana.Backtest.CryptoConverter.Options;
using Banana.Backtest.CryptoConverter.Scheduler;
using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using Hangfire.Prometheus.NetCore;
using MongoDB.Driver;

namespace Banana.Backtest.CryptoConverter.Extensions;

public static class SchedulerExtensions
{
    public static void ConfigureHangfire(this IServiceCollection services, IConfiguration configuration)
    {
        var converterOptions = configuration.GetOptions<ConverterOptions>();
        var mongoDbOptions = configuration.GetOptions<MongoOptions>();

        services.AddHangfire((provider, hangfire) =>
        {
            hangfire.UseSerilogLogProvider();
            hangfire.UseActivator(HangfireDefaults.JobActivator(provider));
            hangfire.UseSimpleAssemblyNameTypeSerializer();
            hangfire.UseDefaultCulture(CultureInfo.InvariantCulture, CultureInfo.InvariantCulture);
            hangfire.UseSerializerSettings(HangfireDefaults.JsonSerializerSettings);
            hangfire.UseMongoStorage(
                provider.GetRequiredService<IMongoClient>(),
                mongoDbOptions.DatabaseName,
                new MongoStorageOptions
                {
                    MigrationOptions = new MongoMigrationOptions
                    {
                        BackupPostfix = mongoDbOptions.BackupPostfix,
                        MigrationStrategy = new DropMongoMigrationStrategy(),
                        BackupStrategy = new CollectionMongoBackupStrategy()
                    },
                    Prefix = mongoDbOptions.Prefix,
                    CheckConnection = true
                });
        });
        services.AddHangfireServer((provider, server) =>
        {
            server.WorkerCount = converterOptions.WorkersCount;
            server.ServerName = converterOptions.ServerName;
            server.MaxDegreeOfParallelismForSchedulers = converterOptions.MaxDegreeOfParallelismForSchedulers;
            server.Queues = converterOptions.Queues;
            server.TaskScheduler = provider.GetRequiredService<AffinitizedThreadPoolTaskScheduler>();
        });
        services.AddSingleton<AffinitizedThreadPoolTaskScheduler>();
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
            DefaultRecordsPerPage = 250,
            Authorization = [EmptyDashboardAuthorizationFilter.Instance]
        });
    }
}
