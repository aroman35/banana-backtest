using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Binance.Net.Objects.Models.Futures;
using Microsoft.Extensions.Caching.Memory;

namespace Banana.Strategies.MeanReverse.Binance.Extensions;

public static class InstrumentsExtensions
{
    public static decimal MinOrderQuantity(this BinanceFuturesSymbol instrument, decimal price)
    {
        if (price == 0)
            return 0;
        var minQuantityUnfiltered = instrument.MinNotionalFilter!.MinNotional / price;
        var delta = instrument.LotSizeFilter!.StepSize - minQuantityUnfiltered % instrument.LotSizeFilter!.StepSize;
        var quantity = minQuantityUnfiltered + delta;
        return quantity;
    }

    public static decimal RoundPrice(this BinanceFuturesSymbol instrument, decimal price, Side side)
    {
        return decimal.Round(
            price,
            instrument.PricePrecision,
            side is Side.Long ? MidpointRounding.ToPositiveInfinity : MidpointRounding.ToNegativeInfinity);
    }

    public static void SetForSymbol<TType>(this IMemoryCache memoryCache, string symbol, TType value)
    {
        var key = symbol.CacheKey<TType>();
        memoryCache.Set(key, value);
    }

    public static bool InvalidateForSymbol<TType>(this IMemoryCache memoryCache, string symbol, out TType? value)
    {
        var key = symbol.CacheKey<TType>();
        value = memoryCache.Get<TType>(key);
        if (value is null)
            return false;
        memoryCache.Remove(key);
        return true;
    }

    public static TType GetForSymbol<TType>(this IMemoryCache memoryCache, string symbol)
    {
        var key = symbol.CacheKey<TType>();
        var item = memoryCache.Get<TType>(key);
        ArgumentNullException.ThrowIfNull(item);
        return item;
    }

    private static string CacheKey<TType>(this string symbol)
    {
        return $"{Helpers.FriendlyTypeName<TType>()}_{symbol.ToLowerInvariant()}";
    }
}
