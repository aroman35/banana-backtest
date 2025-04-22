using Banana.Backtest.Common.Models;
using Banana.Backtest.Emulator.ExchangeEmulator;

namespace Banana.Backtest.Emulator.Contracts;

/// <summary>
/// Запрос на выставление заявки
/// </summary>
public struct PlaceOrderRequest
{
    /// <summary>
    /// Направление
    /// </summary>
    public Side Side;

    /// <summary>
    /// Количество
    /// </summary>
    public double Quantity;

    /// <summary>
    /// Цена
    /// </summary>
    public double Price;

    /// <summary>
    /// Клиентский ИД
    /// </summary>
    public Guid ClientOrderId;

    /// <summary>
    /// Тип
    /// </summary>
    public OrderType OrderType;
}
