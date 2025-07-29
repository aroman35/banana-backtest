using Banana.Strategies.MeanReverse.Binance.Extensions;

namespace Banana.Strategies.MeanReverse.Binance.Persistence;

/// <summary>
/// Для продакшн-сборки необходимо убедиться что папка с отчетами будет очищаться
/// </summary>
public class ReportsProviderService(TimeProvider clock, IServiceScopeFactory serviceScopeFactory, ILogger logger) : BackgroundService
{
    private readonly ILogger _logger = logger.ForContext<ReportsProviderService>();

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var now = clock.GetUtcNow();
        var timeTillDayEnd = clock.TodayShift(TimeSpan.FromDays(1)) - now + TimeSpan.FromMinutes(1);

        await Task.Delay(timeTillDayEnd, clock, stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1), clock);
        do
        {
            try
            {
                await using (var scope = serviceScopeFactory.CreateAsyncScope())
                {
                    var reportsProvider = scope.ServiceProvider.GetRequiredService<ReportsProvider>();
                    await reportsProvider.SaveDailyReport(stoppingToken);
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Shit has happened during report creation");
            }
        } while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }
}
