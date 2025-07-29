namespace Banana.Backtest.Launcher.Infrastructure;

public class BacktestServiceProviderBuilder(IServiceCollection services)
{
    public IServiceCollection ServiceCollection { get; set; } = services;
}
