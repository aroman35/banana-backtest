using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Banana.Backtest.Common.Models.Root;

[StructLayout(LayoutKind.Sequential)]
public struct Symbol : IEquatable<Symbol>
{
    private const char CLASS_CODE_SEPARATOR = '@';
    private static readonly ConcurrentDictionary<Symbol, string> _symbolToTickerMap = new();
    private static readonly ConcurrentDictionary<Symbol, string> _symbolToClassCodeMap = new();
    private static readonly ConcurrentDictionary<Symbol, string> _symbolToCryptoSymbolStringMap = new();
    private static readonly ConcurrentDictionary<Symbol, string> _symbolToFullStringMap = new();
    private Asset _baseAsset;
    private Asset _quoteAsset;
    private Exchange _exchange;

    public const string SYMBOL_FORMAT = "{BaseAsset}@{QuoteAsset}.{Exchange}";
    
    public string Ticker
    {
        get
        {
            return _symbolToTickerMap.GetOrAdd(
                this,
                symbol => symbol._baseAsset.ToString());
        }
    }

    public string ClassCode
    {
        get
        {
            return _symbolToClassCodeMap.GetOrAdd(
                this,
                symbol => symbol._quoteAsset.ToString());
        }
    }

    public string CryptoSymbolString =>
        _symbolToCryptoSymbolStringMap.GetOrAdd(
            this,
            SymbolCryptoString);

    public Exchange Exchange => _exchange;

    public bool IsCrypto => (Exchange.Crypto & _exchange) == _exchange;
    
    public static Symbol Parse(string ticker, string classCode, Exchange exchange)
    {
        return new Symbol
        {
            _baseAsset = Asset.Get(ticker),
            _quoteAsset = Asset.Get(classCode),
            _exchange = exchange
        };
    }

    public static Symbol Create(Asset baseAsset, Asset quoteAsset, Exchange exchange)
    {
        return new Symbol
        {
            _baseAsset = baseAsset,
            _quoteAsset = quoteAsset,
            _exchange = exchange
        };
    }

    public static Symbol Parse(ReadOnlySpan<char> symbolSpan, Exchange? givenExchange = null)
    {
        try
        {
            var indexOfClassCode = symbolSpan.IndexOf(CLASS_CODE_SEPARATOR);
            var indexOfExchange = symbolSpan.IndexOf('.');
            if (indexOfClassCode < 0)
                throw new InvalidOperationException($"Invalid symbol format: {symbolSpan}, '@' was not found");
            if (indexOfExchange < 0 && givenExchange == null)
                throw new InvalidOperationException($"Invalid symbol format: {symbolSpan}, exchange was not specified");

            var baseAssetSpan = symbolSpan[..indexOfClassCode];
            var quoteAssetSpan = symbolSpan.Slice(
                indexOfClassCode + 1,
                indexOfExchange > 0
                    ? indexOfExchange - indexOfClassCode - 1
                    : symbolSpan.Length - indexOfClassCode - 1);
            var baseAsset = Asset.Get(baseAssetSpan);
            var quoteAsset = Asset.Get(quoteAssetSpan);
            var exchange = givenExchange ?? Enum.Parse<Exchange>(symbolSpan[(indexOfExchange + 1)..]);

            return new Symbol
            {
                _baseAsset = baseAsset,
                _quoteAsset = quoteAsset,
                _exchange = exchange
            };
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Unable to parse symbol format: {symbolSpan}", exception);
        }
    }

    public static Symbol Parse(string symbolString, Exchange? exchange = null)
    {
        return Parse(symbolString.AsSpan(), exchange);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Symbol other)
    {
        return _exchange == other._exchange && _quoteAsset == other._quoteAsset && _baseAsset == other._baseAsset;
    }
    
    public override bool Equals(object? obj)
    {
        return obj is Symbol other && Equals(other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Symbol a, Symbol b) => a.Equals(b);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Symbol a, Symbol b) => !a.Equals(b);

    public override string ToString()
    {
        return _symbolToFullStringMap.GetOrAdd(this, symbol => $"{symbol._baseAsset}@{symbol._quoteAsset}.{symbol._exchange}");
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_baseAsset, _quoteAsset, _exchange);
    }

    public static string SymbolCryptoString(Symbol symbol)
    {
        switch (symbol._exchange)
        {
            case Exchange.OkexSwap:
                const string swapSuffix = "SWAP";
                return string.Join('-', symbol.Ticker, symbol.ClassCode, swapSuffix);
            case Exchange.KucoinFutures:
                const char kucoinFuturesSuffix = 'M';
                return symbol.Ticker + symbol.ClassCode + kucoinFuturesSuffix;
            case Exchange.KucoinSpot:
                return string.Join('-', symbol.Ticker, symbol.ClassCode);
            default: return symbol.Ticker + symbol.ClassCode;
        }
    }
}