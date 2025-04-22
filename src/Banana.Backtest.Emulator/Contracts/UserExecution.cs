using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.ExchangeEmulator;

namespace Banana.Backtest.Emulator.Contracts;

/// <summary>
/// Клиентская сделка
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct UserExecution : IEquatable<UserExecution>
{
    /// <summary>
    /// Биржевой ИД
    /// </summary>
    public long TradeId;

    /// <summary>
    /// Биржевой ИД соответствующей заявки
    /// </summary>
    public long OrderId;

    /// <summary>
    /// Направление
    /// </summary>
    public Side Side;

    /// <summary>
    /// Цена исполнения
    /// </summary>
    public double ExecutionPrice;

    /// <summary>
    /// Исполненное количество
    /// </summary>
    public double ExecutedQuantity;

    /// <summary>
    /// Биржевое время исполнения
    /// </summary>
    public long Timestamp;

    /// <summary>
    /// Клиентский ИД соответствующей заявки
    /// </summary>
    public Guid ClientOrderId;

    /// <summary>
    /// Тип исполнения (Maker/Taker)
    /// </summary>
    public bool IsMaker;

    /// <summary>
    /// Дефолт
    /// </summary>
    public static UserExecution None => new();

    /// <summary>
    /// Создание сделки на основе заявки
    /// </summary>
    /// <param name="order">Ссылка на состояние клиентской заявки</param>
    /// <param name="executionPrice">Цена исполнения</param>
    /// <param name="executedQuantity">Исполненное количество</param>
    /// <param name="timestamp">Биржевое время исполнения</param>
    /// <param name="isMaker">Тип исполнения</param>
    /// <returns>Полученная сделка</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UserExecution FillOrder(ref OrderInfo order, double executionPrice, double executedQuantity,
        long timestamp, bool isMaker = false)
    {
        order = order.PartiallyFill(executedQuantity, timestamp);
        return new UserExecution
        {
            TradeId = Helpers.NextId,
            OrderId = order.Id,
            Side = order.Side,
            ExecutionPrice = executionPrice,
            ExecutedQuantity = executedQuantity,
            Timestamp = timestamp,
            ClientOrderId = order.ClientOrderId,
            IsMaker = isMaker
        };
    }

    /// <summary>
    /// Преобразование в обезличенную сделку
    /// </summary>
    /// <returns>Обезличенная сделка</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MarketDataItem<TradeUpdate> ToMarketDataItem()
    {
        return new MarketDataItem<TradeUpdate>(new TradeUpdate
        {
            Price = ExecutionPrice,
            Quantity = ExecutedQuantity,
            Side = Side,
            TradeId = TradeId,
        }, Timestamp);
    }

    [Obsolete]
    public static UserExecution OrderFullFill(UserOrder order, long timestamp, double meanPrice)
    {
        return new UserExecution
        {
            TradeId = Helpers.NextId,
            OrderId = order.Id,
            Side = order.Side,
            ExecutionPrice = meanPrice,
            ExecutedQuantity = order.Quantity,
            Timestamp = timestamp,
            ClientOrderId = order.ClientOrderId
        };
    }

    [Obsolete]
    public static unsafe UserExecution OrderPartiallyFill(UserOrder* orderPtr, double executedQuantity, long timestamp)
    {
        var execution = new UserExecution
        {
            TradeId = Helpers.NextId,
            OrderId = orderPtr->Id,
            Side = orderPtr->Side,
            ExecutionPrice = orderPtr->Price,
            ExecutedQuantity = executedQuantity,
            Timestamp = timestamp,
            ClientOrderId = orderPtr->ClientOrderId
        };
        *orderPtr = orderPtr->PartiallyFill(executedQuantity);
        return execution;
    }

    public bool Equals(UserExecution other)
    {
        return TradeId == other.TradeId;
    }

    public override bool Equals(object? obj)
    {
        return obj is UserExecution other && Equals(other);
    }

    public override int GetHashCode()
    {
        return TradeId.GetHashCode();
    }
}
