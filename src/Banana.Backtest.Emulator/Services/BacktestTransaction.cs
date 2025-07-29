namespace Banana.Backtest.Emulator.Services;

public class BacktestTransaction : IBacktestTransaction
{
    private IBacktestTransaction? _innerTransaction;
    private readonly TimeProvider _timeProvider;

    private BacktestTransaction(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public static BacktestTransaction Create(TimeProvider timeProvider)
    {
        return new BacktestTransaction(timeProvider);
    }

    public static BacktestTransaction Join(IBacktestTransaction transaction)
    {
        if (transaction is not BacktestTransaction backtestTransaction)
            throw new ArgumentException("Transaction must be of type backtestTransaction", nameof(transaction));
        var instance = new BacktestTransaction(backtestTransaction._timeProvider);
        instance._innerTransaction =  transaction;
        return instance;
    }

    public void Dispose()
    {
        Commit();
    }

    public void Commit()
    {
    }

    public void Rollback()
    {
    }

    public void Rollback(Exception exception)
    {
    }
}
