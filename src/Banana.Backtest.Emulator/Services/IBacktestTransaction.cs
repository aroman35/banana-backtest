namespace Banana.Backtest.Emulator.Services;

public interface IBacktestTransaction : IDisposable
{
    void Commit();
    void Rollback();
    void Rollback(Exception exception);
}
