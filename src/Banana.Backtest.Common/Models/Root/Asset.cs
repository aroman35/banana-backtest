using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Banana.Backtest.Common.Extensions.Helpers;

namespace Banana.Backtest.Common.Models.Root;

[StructLayout(LayoutKind.Sequential)]
public struct Asset : IEquatable<Asset>, IEquatable<string>
{
    private static readonly ConcurrentDictionary<Asset, string> _assetToStringMap = new();
    private static readonly ConcurrentDictionary<string, Asset> _stringToAssetMap = new();
    private ulong _asset;
    
    public static readonly Asset USDT = Parse("USDT");
    public static readonly Asset USD = Parse("USD");
    public static readonly Asset BTC = Parse("BTC");
    public static readonly Asset ETH = Parse("ETH");
    public static readonly Asset BNB = Parse("BNB");
    public static readonly Asset SPBFUT = Parse("SPBFUT");
    public static readonly Asset TQBR = Parse("TQBR");

    static Asset()
    {
        _stringToAssetMap["USDT"] = USDT;
        _stringToAssetMap["USD"] = USD;
        _stringToAssetMap["BTC"] = BTC;
        _stringToAssetMap["ETH"] = ETH;
        _stringToAssetMap["BNB"] = BNB;
        _stringToAssetMap["SPBFUT"] = SPBFUT;
        _stringToAssetMap["TQBR"] = TQBR;
    }

    public override string ToString()
    {
        return _assetToStringMap.GetOrAdd(
            this,
            asset => DecodeString(asset._asset));
    }

    public static Asset Get(ReadOnlySpan<char> asset) => _stringToAssetMap.GetOrAdd(asset.ToString(), Parse);

    public static Asset Parse(Span<byte> asciiBytes)
    {
        return new Asset
        {
            _asset = EncodeString(asciiBytes)
        };
    }

    private static Asset Parse(string asset)
    {
        return Parse(asset.AsSpan());
    }

    private static Asset Parse(ReadOnlySpan<char> asset)
    {
        return new Asset
        {
            _asset = EncodeString(asset)
        };
    }


    #region Equality

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Asset other)
    {
        return _asset == other._asset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals([NotNullWhen(true)]object? obj)
    {
        return obj is Asset asset && Equals(asset);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals([NotNullWhen(true)]string? rawAssetValue)
    {
        return !string.IsNullOrWhiteSpace(rawAssetValue) && Equals(Parse(rawAssetValue));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Asset a, Asset b) => a.Equals(b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Asset a, Asset b) => !a.Equals(b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(string a, Asset b) => b.Equals(a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(string a, Asset b) => !b.Equals(a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Asset a, string b) => a.Equals(b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Asset a, string b) => !a.Equals(b);
    
    public override int GetHashCode()
    {
        return _asset.GetHashCode();
    }
    
    #endregion
}