using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.CryptoConverter.Options;
using Banana.Backtest.CryptoConverter.Scheduler.Jobs;
using Hangfire;
using Microsoft.Extensions.Options;

namespace Banana.Backtest.CryptoConverter.Scheduler;

public class JobsScheduleInitializer(IRecurringJobManager recurringJobManager, IOptions<ConverterOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var exchange in options.Value.Exchanges)
        {
            EnqueueRefreshInstrumentsJob(exchange);
            EnqueueConverterJob(exchange);
        }

        return Task.CompletedTask;
    }

    private void EnqueueConverterJob(Exchange exchange)
    {
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

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
