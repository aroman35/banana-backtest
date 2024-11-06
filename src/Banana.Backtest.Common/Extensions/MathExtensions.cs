namespace Banana.Backtest.Common.Extensions;

using System.Runtime.CompilerServices;

public static class MathExtensions
{
    public const double PRECISION = 0.000000005d;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    public static bool IsGreater(this double d1, double d2, double e = PRECISION) => d1 - d2 > e;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLower(this double d1, double d2, double e = PRECISION) => d2 - d1 > e;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsGreaterOrEquals(this double d1, double d2, double e = PRECISION) => d1 - d2 > e || d1.IsEquals(d2, e);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLowerOrEquals(this double d1, double d2, double e = PRECISION) => d2 - d1 > e || d1.IsEquals(d2, e);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEquals(this double d1, double d2, double e = PRECISION) => Math.Abs(d1 - d2) < e;
}
