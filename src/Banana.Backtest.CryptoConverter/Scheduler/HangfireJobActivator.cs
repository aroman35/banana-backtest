using System.Diagnostics;
using Hangfire;
using Hangfire.Server;

namespace Banana.Backtest.CryptoConverter.Scheduler;

public class HangfireJobActivator(IServiceScopeFactory serviceScopeFactory) : JobActivator
{
    public override object ActivateJob(Type jobType)
    {
        Debug.Print(
            "Not managed scope was called in a case of resolving service '{0}' from DI using a {1}. " +
            "Ensure the Dispose() method was called on service '{0}'.",
            jobType.Name,
            nameof(HangfireJobActivator));
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
            if (serviceScope is IAsyncDisposable asyncDisposable)
            {
#pragma warning disable CA2012
                asyncDisposable.DisposeAsync().GetAwaiter().GetResult();
#pragma warning restore CA2012
                return;
            }
            serviceScope.Dispose();
        }
    }
}
