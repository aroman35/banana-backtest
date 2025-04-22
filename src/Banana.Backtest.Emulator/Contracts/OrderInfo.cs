using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Emulator.ExchangeEmulator;

namespace Banana.Backtest.Emulator.Contracts;

/// <summary>
/// Состояние заявки
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct OrderInfo
{
    /// <summary>
    /// Биржевой ИД
    /// </summary>
    public long Id;

    /// <summary>
    /// Тип
    /// </summary>
    public OrderType Type;

    /// <summary>
    /// Направление
    /// </summary>
    public Side Side;

    /// <summary>
    /// Цена выставления. Обязательна для Type=OrderType.Limit
    /// </summary>
    public double Price;

    /// <summary>
    /// Время обработки заявки биржей
    /// </summary>
    public long Timestamp;

    /// <summary>
    /// Клиентский ИД
    /// </summary>
    public Guid ClientOrderId;

    /// <summary>
    /// Количество при выставлении
    /// </summary>
    public double RequestedQuantity;

    /// <summary>
    /// Количество, которое было исполнено
    /// </summary>
    public double ExecutedQuantity;

    /// <summary>
    /// Оставшееся количество
    /// </summary>
    public double RemainingQuantity;

    /// <summary>
    /// Время обновления статуса
    /// </summary>
    public long StatusUpdateTimestamp;

    /// <summary>
    /// Статус
    /// </summary>
    public OrderStatus Status;

    /// <summary>
    /// Код причины отклонения
    /// </summary>
    public RejectionReason RejectionReason;

    /// <summary>
    /// Успешное создание на основе запроса пользователя
    /// </summary>
    /// <param name="request">Запрос пользователя</param>
    /// <param name="timestamp">Время обработки</param>
    /// <returns>Полученное состояние</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OrderInfo Accept(PlaceOrderRequest request, long timestamp)
    {
        return new OrderInfo
        {
            Id = Helpers.NextId,
            Type = request.OrderType,
            Side = request.Side,
            Price = request.Price,
            RequestedQuantity = request.Quantity,
            Timestamp = timestamp,
            ClientOrderId = request.ClientOrderId,
            ExecutedQuantity = 0.0D,
            RemainingQuantity = request.Quantity,
            Status = OrderStatus.New,
            StatusUpdateTimestamp = timestamp,
        };
    }

    /// <summary>
    /// Неуспешное создание на основе отклонения биржей
    /// </summary>
    /// <param name="request">Запрос пользователя</param>
    /// <param name="timestamp">Время обработки</param>
    /// <param name="reason">Код отмены</param>
    /// <returns>Полученное состояние</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OrderInfo Reject(PlaceOrderRequest request, long timestamp, RejectionReason reason)
    {
        return new OrderInfo
        {
            Id = Helpers.NextId,
            Type = request.OrderType,
            Side = request.Side,
            Price = request.Price,
            RequestedQuantity = request.Quantity,
            Timestamp = timestamp,
            ClientOrderId = request.ClientOrderId,
            ExecutedQuantity = 0.0D,
            RemainingQuantity = request.Quantity,
            Status = OrderStatus.Rejected,
            StatusUpdateTimestamp = timestamp,
            RejectionReason = reason
        };
    }

    /// <summary>
    /// Отклонение заявки после ее первичной успешной обработки
    /// </summary>
    /// <param name="timestamp">Время обработки</param>
    /// <param name="reason">Причина отмены</param>
    /// <returns>Обновленное состояние</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OrderInfo Reject(long timestamp, RejectionReason reason)
    {
        return this with
        {
            Status = OrderStatus.Rejected,
            RejectionReason = reason,
            StatusUpdateTimestamp = timestamp
        };
    }

    /// <summary>
    /// Частичное исполнение
    /// </summary>
    /// <param name="quantity">Исполненное количество</param>
    /// <param name="timestamp">Время обработки</param>
    /// <returns>Обновленное состояние</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OrderInfo PartiallyFill(double quantity, long timestamp)
    {
        var isFullFill = quantity.IsGreaterOrEquals(RemainingQuantity);

        return this with
        {
            Status = isFullFill ? OrderStatus.Fill : OrderStatus.PartiallyFill,
            ExecutedQuantity = isFullFill ? RequestedQuantity : ExecutedQuantity + quantity,
            RemainingQuantity = isFullFill ? 0.0D : RemainingQuantity - quantity,
            StatusUpdateTimestamp = timestamp
        };
    }

    /// <summary>
    /// Отмена заявки (пользователем или биржей) TODO: объединить с методом Reject(long timestamp, RejectionReason reason)
    /// </summary>
    /// <param name="timestamp">Время обработки</param>
    /// <returns>Обновленное состояние</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OrderInfo Cancel(long timestamp)
    {
        return this with
        {
            Status = OrderStatus.Cancelled,
            StatusUpdateTimestamp = timestamp
        };
    }
}
