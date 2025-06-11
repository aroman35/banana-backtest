using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.CryptoConverter.Options;
using Banana.Backtest.CryptoConverter.Scheduler.Jobs;
using Hangfire;
using Microsoft.Extensions.Options;

namespace Banana.Backtest.CryptoConverter.Scheduler;

public class JobsScheduleInitializer(
    IRecurringJobManagerV2 recurringJobManager,
    IOptions<ConverterOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var exchange in options.Value.Exchanges)
        {
            EnqueueRefreshInstrumentsJob(exchange);
            EnqueueConverterJob(exchange);
        }

        EnqueueReconciliationJob();
        EnqueueMetaBuildJob();

        return Task.CompletedTask;
    }

    private void EnqueueConverterJob(Exchange exchange)
    {
        if (string.IsNullOrEmpty(options.Value.DownloadScheduleCrone))
            return;
        var jobName = $"CONVERT_EXCHANGE_{exchange.ToString().ToUpper().Replace('-', '_')}";
        recurringJobManager.AddOrUpdate<ExchangeConverterJob>(
            jobName,
            HangfireDefaults.CONVERT_EXCHANGE_QUEUE,
            service => service.HandleAsync(new RunAllInstrumentsHandlingCommand(exchange), CancellationToken.None),
            options.Value.DownloadScheduleCrone,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }

    private void EnqueueRefreshInstrumentsJob(Exchange exchange)
    {
        if (string.IsNullOrEmpty(options.Value.RefreshInstrumentsScheduleCrone))
            return;
        var jobName = $"REFRESH_INSTRUMENTS_{exchange.ToString().ToUpper().Replace('-', '_')}";
        recurringJobManager.AddOrUpdate<RefreshInstrumentsJob>(
            jobName,
            HangfireDefaults.INSTRUMENTS_REFRESH_QUEUE,
            service => service.HandleAsync(exchange, CancellationToken.None),
            options.Value.RefreshInstrumentsScheduleCrone,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }

    private void EnqueueReconciliationJob()
    {
        if (string.IsNullOrEmpty(options.Value.ReconciliationScheduleCrone))
            return;
        var jobName = "RECONCILIATION";
        recurringJobManager.AddOrUpdate<ReconciliationJob>(
            jobName,
            HangfireDefaults.RECONCILIATION_QUEUE,
            service => service.Handle(),
            options.Value.ReconciliationScheduleCrone,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }

    private void EnqueueMetaBuildJob()
    {
        if (string.IsNullOrEmpty(options.Value.MetaBuildScheduleCrone))
            return;
        var jobName = "META_BUILD";
        recurringJobManager.AddOrUpdate<MetaBuildJobLauncher>(
            jobName,
            HangfireDefaults.META_BUILD_QUEUE,
            service => service.Handle(),
            options.Value.MetaBuildScheduleCrone,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
