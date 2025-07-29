using System.Collections;
using System.Collections.Concurrent;

namespace Banana.Backtest.Launcher.Infrastructure;

public class BacktestAsyncServiceScope(IServiceProvider rootProvider) :
    IServiceScope,
    IAsyncDisposable,
    IKeyedServiceProvider,
    IServiceScopeFactory
{
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<ServiceKey, object?> _resolvedServices = new();
    private readonly List<IDisposable> _disposables = new();
    private readonly List<IAsyncDisposable> _asyncDisposables = new();

    public IServiceProvider ServiceProvider => this;

    public object? GetService(Type serviceType)
    {
        var key = new ServiceKey(serviceType);
        return _resolvedServices.GetOrAdd(key, _ => ResolveService(serviceType));
    }

    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        var key = new ServiceKey(serviceType, serviceKey);
        return _resolvedServices.GetOrAdd(key, _ => ResolveService(serviceType, serviceKey));
    }

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
    {
        var key = new ServiceKey(serviceType, serviceKey);
        var service = _resolvedServices.GetOrAdd(key, _ => ResolveService(serviceType, serviceKey));
        ArgumentNullException.ThrowIfNull(service);
        return service;
    }

    public IServiceScope CreateScope() => rootProvider.CreateScope();

    private record ServiceKey(Type ServiceType, object? Key = null);

    public void Dispose()
    {
        Parallel.ForEach(_disposables, service => service.Dispose());
        _disposables.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Parallel.ForEachAsync(_asyncDisposables, (service, _) => service.DisposeAsync());
        _asyncDisposables.Clear();
        _resolvedServices.Clear();
        GC.SuppressFinalize(this);
    }

    private object? ResolveService(Type serviceType, object? serviceKey = null)
    {
        var key = new ServiceKey(serviceType, serviceKey);
        using (_lock.EnterScope())
        {
            if (_resolvedServices.TryGetValue(key, out var service))
                return service;

            if (serviceType.IsAssignableTo(typeof(IEnumerable)))
            {
                var genericType = serviceType.GetGenericArguments().Single();
                var resolvedServices = (serviceKey is null
                    ? rootProvider.GetServices(genericType)
                    : rootProvider.GetKeyedServices(genericType, serviceKey));
                foreach (var resolvedService in resolvedServices)
                {
                    if (resolvedService is IDisposable disposable)
                        _disposables.Add(disposable);
                    if (resolvedService is IAsyncDisposable asyncDisposable)
                        _asyncDisposables.Add(asyncDisposable);
                }
                return resolvedServices;
            }
            else
            {
                var resolvedService = serviceKey is null
                    ? rootProvider.GetService(serviceType)
                    : rootProvider.GetRequiredKeyedService(serviceType, serviceKey);
                if (resolvedService is IDisposable disposable)
                    _disposables.Add(disposable);
                if (resolvedService is IAsyncDisposable asyncDisposable)
                    _asyncDisposables.Add(asyncDisposable);
                return resolvedService;
            }
        }
    }
}
