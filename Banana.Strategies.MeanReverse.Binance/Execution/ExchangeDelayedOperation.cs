namespace Banana.Strategies.MeanReverse.Binance.Execution;

public class ExchangeDelayedOperation : IDisposable
{
    private readonly ManualResetEventSlim _manualResetEventSlim;

    private ExchangeDelayedOperation(ManualResetEventSlim manualResetEventSlim, CancellationToken cancellationToken)
    {
        _manualResetEventSlim = manualResetEventSlim;
        _manualResetEventSlim.Wait(cancellationToken);
        _manualResetEventSlim.Reset();
    }

    public static IDisposable Perform(ManualResetEventSlim manualResetEventSlim, CancellationToken cancellationToken = default)
    {
        return new ExchangeDelayedOperation(manualResetEventSlim, cancellationToken);
    }

    public void Dispose()
    {
        _manualResetEventSlim.Set();
    }
}
