using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Banana.Strategies.MeanReverse.Binance.DataFlow;

public class DataFlowMediator(IServiceProvider serviceProvider)
{
    public ValueTask SendForSymbol<T>(T data, string symbol)
    {
        return serviceProvider
            .GetRequiredKeyedService<ChannelWriter<T>>(symbol).WriteAsync(data);
    }

    public ValueTask Send<T>(T data)
    {
        return serviceProvider
            .GetRequiredService<ChannelWriter<T>>().WriteAsync(data);
    }

    public IAsyncEnumerable<T> StreamForSymbol<T>(string symbol, CancellationToken token = default)
    {
        return serviceProvider
            .GetRequiredKeyedService<ChannelReader<T>>(symbol).ReadAllAsync(token);
    }

    public IAsyncEnumerable<T> Stream<T>(CancellationToken token = default)
    {
        return serviceProvider
            .GetRequiredService<ChannelReader<T>>().ReadAllAsync(token);
    }

    private static Channel<T> CreateUnboundedChannel<T>()
    {
        return Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = true,
            SingleReader = false,
            SingleWriter = true
        });
    }

    private static Channel<T> GetOrAddChannelFromPool<T>(ConcurrentDictionary<string, Channel<T>> channelsPool, string key)
    {
        return channelsPool.GetOrAdd(key, _ => CreateUnboundedChannel<T>());
    }
}
