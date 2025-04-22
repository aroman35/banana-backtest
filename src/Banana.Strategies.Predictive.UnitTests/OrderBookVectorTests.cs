using Banana.Backtest.Common.Extensions;
using Banana.Strategies.Predictive.Simd;

namespace Banana.Strategies.Predictive.UnitTests;

public class OrderBookVectorTests
{
    [Fact]
    public unsafe void VectorizedOrderBookCreation()
    {
        var orderBookSnapshot = new OrderBookDepth20
        {
            Timestamp = DateTime.Parse("2020-02-02T01:01:01Z").ToUnixTimeMilliseconds()
        };


        for (var i = 0; i < 20; i++)
        {
            orderBookSnapshot.BidPrices[i] = (21 - i) * 100.0;
            orderBookSnapshot.BidQuantities[i] = 100.0 * (i + 1);

            orderBookSnapshot.AskPrices[i] = (i + 2.2) * 1000.0;
            orderBookSnapshot.AskQuantities[i] = 100.0 * (i + 1);
        }

        var vectorizedOrderBook = new OrderBookDepth16Vector(&orderBookSnapshot);

        Assert.Equal(vectorizedOrderBook.MidPrice, 2150, MathExtensions.PRECISION);
        Assert.Equal(vectorizedOrderBook.Bids.BestPrice, 2100, MathExtensions.PRECISION);
        Assert.Equal(vectorizedOrderBook.Bids.VolumesUpper[0], 210_000, MathExtensions.PRECISION);
        Assert.Equal(vectorizedOrderBook.Bids.VolumesUpper[7], 1_120_000, MathExtensions.PRECISION);
        Assert.Equal(vectorizedOrderBook.Bids.VolumesLower[0], 1_170_000, MathExtensions.PRECISION);
        Assert.Equal(vectorizedOrderBook.Bids.VolumesLower[7], 960_000, MathExtensions.PRECISION);

        Assert.Equal(vectorizedOrderBook.Asks.BestPrice, 2200, MathExtensions.PRECISION);
        Assert.Equal(vectorizedOrderBook.Asks.VolumesUpper[0], 220_000, MathExtensions.PRECISION);
        Assert.Equal(vectorizedOrderBook.Asks.VolumesUpper[7], 7_360_000, MathExtensions.PRECISION);
        Assert.Equal(vectorizedOrderBook.Asks.VolumesLower[0], 9_180_000, MathExtensions.PRECISION);
        Assert.Equal(vectorizedOrderBook.Asks.VolumesLower[7], 27_520_000, MathExtensions.PRECISION);
    }
}
