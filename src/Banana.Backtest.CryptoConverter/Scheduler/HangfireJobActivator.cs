using Hangfire;
using Hangfire.Server;

namespace Banana.Backtest.CryptoConverter.Scheduler;

public class HangfireJobActivator(IServiceScopeFactory serviceScopeFactory, ILogger logger) : JobActivator
{
    private readonly ILogger _logger = logger.ForContext<HangfireJobActivator>();
    public override object ActivateJob(Type jobType)
    {
        _logger.Warning("Not managed scope were called to the hangfire job activator");
        return new MicrosoftDependencyInjectionJobActivatorScope(serviceScopeFactory.CreateScope()).Resolve(jobType);
    }

    public override JobActivatorScope BeginScope(PerformContext context)
    {
        return new MicrosoftDependencyInjectionJobActivatorScope(serviceScopeFactory.CreateScope());
    }

    private class MicrosoftDependencyInjectionJobActivatorScope(IServiceScope serviceScope) : JobActivatorScope
    {
        public override object Resolve(Type type)
        {
            var service = serviceScope.ServiceProvider.GetRequiredService(type);
            return service;
        }
        
        public override void DisposeScope()
        {
            serviceScope.Dispose();
        }
    }
}

