using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;
using Banana.Backtest.Emulator.ExchangeEmulator;
using Serilog;

namespace Banana.Backtest.Emulator.Services;

/// <summary>
/// Торгуем тейкерами по одному уровню только в том случае если лучшее предложение не хуже установленной цены
/// Если надо исполниться ASAP по любой цене, устанавливаем PriceSpread = 1.0
/// </summary>
public class FastExecution(
    IChannelsProvider channelsProvider,
    TimeProvider timeProvider,
    FastExecutionSettings settings,
    ILogger logger)
    : ExecutionBase<FastExecutionSettings>(channelsProvider, timeProvider, settings, logger)
{
    protected override async ValueTask OnMarketDataUpdated()
    {
        if (!PendingQuantity.IsGreaterOrEquals(0.0D))
            return;

        var bestTakerOffer = Settings.Side is Side.Long
            ? LastOrderBookSnapshot.Ask(1)
            : LastOrderBookSnapshot.Bid(1);

        var canTrade =
            ((Settings.PriceLimit * (1 + Settings.PriceSpread * (int)Settings.Side) - bestTakerOffer.Price) *
             (int)Settings.Side).IsGreaterOrEquals(0.0D);

        if (!canTrade)
            return;

        var quantity = double.Min(Settings.RequestedQuantity - PendingQuantity, bestTakerOffer.Quantity);
        if (quantity.IsEquals(0.0D))
            return;
        await PlaceOrder(new PlaceOrderRequest
        {
            ClientOrderId = Guid.NewGuid(),
            OrderType = OrderType.Limit,
            Price = bestTakerOffer.Price,
            Quantity = quantity,
            Side = Settings.Side
        });
    }

    protected override ValueTask OnStateUpdated()
    {
        if (FilledQuantity.IsEquals(Settings.RequestedQuantity))
            ExecutionCompleted();
        return ValueTask.CompletedTask;
    }
}
