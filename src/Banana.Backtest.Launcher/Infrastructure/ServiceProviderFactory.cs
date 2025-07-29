namespace Banana.Backtest.Launcher.Infrastructure;

public class ServiceProviderFactory : IServiceProviderFactory<BacktestServiceProviderBuilder>
{
    public BacktestServiceProviderBuilder CreateBuilder(IServiceCollection services)
    {
        return new BacktestServiceProviderBuilder(services);
    }

    public IServiceProvider CreateServiceProvider(BacktestServiceProviderBuilder containerBuilder)
    {
        return containerBuilder.ServiceCollection.BuildServiceProvider();
    }
}
