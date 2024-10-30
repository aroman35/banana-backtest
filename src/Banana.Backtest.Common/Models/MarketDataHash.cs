using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.Common.Models;

[StructLayout(LayoutKind.Sequential)]
public struct MarketDataHash : IEquatable<MarketDataHash>
{
    public Symbol Symbol;
    public DateOnly Date;
    public FeedType Feed;

    public static MarketDataHash Create(Symbol ticker, DateOnly date, FeedType feed = FeedType.Unknown)
    {
        return new MarketDataHash
        {
            Symbol = ticker,
            Date = date,
            Feed = feed
        };
    }
    
    public string FilePath(string? directory, string fileExtension = ".dat")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (Feed is FeedType.Unknown)
            throw new ArgumentException($"Unknown feed type: {Feed}");

        var exchangeDirectory = Path.Combine(directory, Symbol.Exchange.ToString());
        if (!Directory.Exists(exchangeDirectory))
            Directory.CreateDirectory(exchangeDirectory);

        var tickerPath = Path.Combine(exchangeDirectory, Symbol.ToString());

        if (!Directory.Exists(tickerPath))
            Directory.CreateDirectory(tickerPath);

        var filePath = Path.Combine(tickerPath, Date.ToString("yyyy-MM-dd")) + $"_{Feed.ToString().ToLowerInvariant()}{fileExtension}";
        return filePath;
    }

    public MarketDataHash SwitchDate(int days)
    {
        return this with { Date = Date.AddDays(days) };
    }

    public MarketDataHash For(FeedType level)
    {
        return this with { Feed = level };
    }

    public override string ToString()
    {
        return $"{Symbol.ToString()}[{Date:dd.MM.yyyy}] {Feed.ToString()}";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(MarketDataHash other)
    {
        return Feed == other.Feed && Date.Equals(other.Date) && Symbol.Equals(other.Symbol);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals([NotNullWhen(true)]object? obj)
    {
        return obj is MarketDataHash other && Equals(other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(MarketDataHash a, MarketDataHash b) => a.Equals(b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(MarketDataHash a, MarketDataHash b) => !a.Equals(b);

    public override int GetHashCode()
    {
        return HashCode.Combine(Symbol, Date, (int)Feed);
    }
}