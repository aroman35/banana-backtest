namespace Banana.Strategies.MeanReverse.Binance.Extensions;

public static class JobsExtensions
{
    public static async void SafeExecute<T>(Func<T, ValueTask> action, T arg, ILogger logger)
    {
        try
        {
            await action(arg);
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Something went wrong in background transaction");
        }
    }

    public static async Task HandleStreamData<TData>(
        IAsyncEnumerable<TData> dataSource,
        Func<TData, CancellationToken, ValueTask> onDataReceived,
        Func<Exception, Task>? onError = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource, nameof(dataSource));
        ArgumentNullException.ThrowIfNull(onDataReceived, nameof(onDataReceived));

        try
        {
            await Task.Yield();
            await foreach (var data in dataSource.WithCancellation(cancellationToken))
            {
                if (data is not null)
                    await onDataReceived(data, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // ignore and close stream
        }
        catch (Exception exception)
        {
            if (onError is not null)
                await onError.Invoke(exception);
        }
    }

    public static void ThrowIfError(CryptoExchange.Net.Objects.Error? error)
    {
        if (error is not null)
            throw new InvalidOperationException($"Error while initiating api request ({error.Code}): {error.Message}");
    }
}
