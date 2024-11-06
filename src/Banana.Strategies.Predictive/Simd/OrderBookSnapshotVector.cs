using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Banana.Strategies.Predictive.Simd;

public readonly unsafe ref struct OrderBookDepth16Vector(OrderBookDepth20* orderBook)
{
    public readonly OrderBookLevels16 Bids = new(orderBook->BidPrices, orderBook->BidQuantities);
    public readonly OrderBookLevels16 Asks = new(orderBook->AskPrices, orderBook->AskQuantities);
    public readonly long Timestamp = orderBook->Timestamp;
    public float MidPrice => (Bids.BestPrice + Asks.BestPrice) / 2f;

    public readonly ref struct OrderBookLevels16
    {
        public OrderBookLevels16(double* prices, double* quantities)
        {
            var pricesBuffer = stackalloc float[8];
            var quantitiesBuffer = stackalloc float[8];
            for (var i = 0; i < 8; i++)
            {
                pricesBuffer[i] = (float)prices[i];
                quantitiesBuffer[i] = (float)quantities[i];
            }

            PricesUpper = Avx2.LoadVector256(pricesBuffer);
            QuantitiesUpper = Avx2.LoadVector256(quantitiesBuffer);

            for (var i = 0; i < 8; i++)
            {
                pricesBuffer[i] = (float)prices[i + 8];
                quantitiesBuffer[i] = (float)quantities[i + 8];
            }

            PricesLower = Avx2.LoadVector256(pricesBuffer);
            QuantitiesLower = Avx2.LoadVector256(quantitiesBuffer);
        }

        public readonly Vector256<float> PricesUpper;
        public readonly Vector256<float> PricesLower;

        public readonly Vector256<float> QuantitiesUpper;
        public readonly Vector256<float> QuantitiesLower;

        public Vector256<float> VolumesUpper => Avx2.Multiply(PricesUpper, QuantitiesUpper);
        public Vector256<float> VolumesLower => Avx2.Multiply(PricesLower, QuantitiesLower);
        public float BestPrice => PricesUpper[0];
    }
}

public unsafe struct OrderBookDepth20
{
    public const int DEPTH = 20;

    public long Timestamp;
    public fixed double BidPrices[DEPTH];
    public fixed double BidQuantities[DEPTH];
    public fixed double AskPrices[DEPTH];
    public fixed double AskQuantities[DEPTH];

    public double BestBidPrice => BidPrices[0];
    public double BestAskPrice => AskPrices[0];
    public double MidPrice => (BestBidPrice + BestAskPrice) / 2;
}
