namespace Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;

public interface ICacheForSymbol<T>
{
    Task WaitForInitialisation { get; }
    IEnumerable<string> Symbols { get; }

    T Get(string symbol);
}
