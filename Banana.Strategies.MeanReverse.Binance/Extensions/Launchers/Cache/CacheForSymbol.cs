using Microsoft.Extensions.Caching.Memory;

namespace Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;

public abstract class CacheForSymbol<T>(
    IMemoryCache memoryCache,
    ILogger logger) : ICacheForSymbol<T>, IHostedService
{
    private readonly ILogger _logger = logger.ForContext<CacheForSymbol<T>>();
    private readonly HashSet<string> _keys = [];
    private readonly TaskCompletionSource _initializationTask = new();
    private Task _updatesTask = null!;

    public Task WaitForInitialisation => _initializationTask.Task;
    public IEnumerable<string> Symbols => _keys;

    public T Get(string symbol) => memoryCache.GetForSymbol<T>(symbol);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Starting loading cache");
        await foreach (var (symbol, item) in LoadData(cancellationToken))
        {
            if (_keys.Add(symbol))
                memoryCache.SetForSymbol(symbol, item);
        }

        _updatesTask = UpdatesTask(cancellationToken);
        _logger.Information("Cache loaded for {Count} symbols", _keys.Count);
        _initializationTask.TrySetResult();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var key in _keys)
        {
            if (memoryCache.InvalidateForSymbol<T>(key, out var value) && value is not null)
            {
                if (value is IDisposable disposable)
                    disposable.Dispose();
                if (value is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();
            }
        }
        _logger.Information("Cache unloaded for {Count} symbols", _keys.Count);
        await _updatesTask;
    }

    protected abstract IAsyncEnumerable<KeyValuePair<string, T>> LoadData(CancellationToken cancellationToken);

    protected virtual Task UpdatesTask(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected void Update(string symbol, T item)
    {
        memoryCache.SetForSymbol(symbol, item);
    }
}
