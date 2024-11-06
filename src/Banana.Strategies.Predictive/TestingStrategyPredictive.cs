using System.Buffers;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.ExchangeEmulator;

namespace Banana.Strategies.Predictive;

public class TestingStrategyPredictive : IStrategy
{
    private readonly SortedDictionary<long, KeyValuePair<double, Vector512<double>>> _ratiosByTimestampWithMidPrice = new();

    public void OrderBookUpdated(MarketDataItem<OrderBookSnapshot> orderBookSnapshot)
    {
        var midPrice = orderBookSnapshot.Item.MidPrice;
        var bidLevelDepths = ArrayPool<double>.Shared.Rent(3);
        var askLevelDepths = ArrayPool<double>.Shared.Rent(3);
        var bidVolumeWeightedAvgPrices = ArrayPool<double>.Shared.Rent(8);
        var askVolumeWeightedAvgPrices = ArrayPool<double>.Shared.Rent(8);

        CalculateOrderBookWeights(orderBookSnapshot.Item.Bids, bidLevelDepths, bidVolumeWeightedAvgPrices);
        CalculateOrderBookWeights(orderBookSnapshot.Item.Asks, askLevelDepths, askVolumeWeightedAvgPrices);
        
        var depthRatios = ArrayPool<double>.Shared.Rent(3);
        var vwapDiff = ArrayPool<double>.Shared.Rent(8);
        
        for (var i = 0; i < 3; i++)
        {
            depthRatios[i] = bidLevelDepths[i] / askLevelDepths[i];
        }

        for (var i = 0; i < 8; i++)
        {
            vwapDiff[i] = (askLevelDepths[i] + bidLevelDepths[i]) / 2 / midPrice - 1.0;
        }

        var ratios = Vector512.Create(depthRatios);
        _ratiosByTimestampWithMidPrice.Add(orderBookSnapshot.Timestamp, new KeyValuePair<double, Vector512<double>>(midPrice, ratios));
        var ratioSums = Vector512<double>.Zero;

        // Add removal for non-actual
        foreach (var historicalRatio in _ratiosByTimestampWithMidPrice.Values)
        {
            ratioSums = Avx512F.Add(ratioSums, historicalRatio.Value);
        }
        var count = Vector512.Create((double)_ratiosByTimestampWithMidPrice.Count);
        var avgRatios = Avx512F.Divide(ratioSums, count);
        
        // defer
        ArrayPool<double>.Shared.Return(bidLevelDepths);
        ArrayPool<double>.Shared.Return(askLevelDepths);
        ArrayPool<double>.Shared.Return(bidVolumeWeightedAvgPrices);
        ArrayPool<double>.Shared.Return(askVolumeWeightedAvgPrices);
        ArrayPool<double>.Shared.Return(depthRatios);
        ArrayPool<double>.Shared.Return(vwapDiff);
    }

    public void AnonymousTradeReceived(MarketDataItem<TradeUpdate> trade)
    {
        throw new NotImplementedException();
    }

    public void UserExecutionReceived(UserExecution userExecution)
    {
        throw new NotImplementedException();
    }

    public void PlaceOrder(UserOrder order)
    {
        throw new NotImplementedException();
    }

    public void SimulationFinished()
    {
        throw new NotImplementedException();
    }

    // Move to order book extensions
    // View order-book levels as Vector512<double> of bidPrices | bidQuantities | askPrices | askQuantities
    private void CalculateOrderBookWeights(OrderBookLevels20 levels, double[] quantities, double[] volumeWeightedAvgPrices)
    {
        // LEVELS_STEPS step = 3; count = 3;
        // VOLUME_STEPS step = 1000; count = 8;
        ReadOnlySpan<int> levelGroups = [3, 6, 9]; // To Options
        ReadOnlySpan<int> volumeGroups = [1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000]; // To Options
        var totalVolume = 0.0;
        var totalQuantity = 0.0;
        for (var i = 0; i < 9; i++)
        {
            var level = levels[i];
            if (level.Quantity.IsEquals(0.0))
                return;
            totalVolume += level.Quantity * level.Quantity;
            totalQuantity += level.Quantity;
            var priceRation = totalVolume / totalQuantity;
            // round to step upper
            var currentLevelGroup = 3 - i % 3 + i;
            var quantityIndex = levelGroups.IndexOf(currentLevelGroup);
            quantities[quantityIndex] = totalQuantity;

            var currentVolumeGroup = (int)double.Round(totalVolume - totalVolume % 1000 + 1000, 0);
            var volumeIndex = volumeGroups.IndexOf(currentVolumeGroup);
            if (volumeIndex >= 0)
            {
                volumeWeightedAvgPrices[volumeIndex] = priceRation;
            }
        }
    }
}