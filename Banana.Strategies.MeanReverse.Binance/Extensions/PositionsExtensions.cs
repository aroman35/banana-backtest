using Banana.Backtest.Common.Models;

namespace Banana.Strategies.MeanReverse.Binance.Extensions;

public static class PositionsExtensions
{
    public static Side Revert(this Side side)
    {
        return (Side)(-(int)side);
    }
}
