namespace Banana.Backtest.Emulator.Contracts;

/// <summary>
/// Коды отмены
/// </summary>
public enum RejectionReason
{
    /// <summary>
    /// Не отменен
    /// </summary>
    None = 0,

    /// <summary>
    /// Дублирующий клиентский ИД
    /// </summary>
    DuplicateClientOrderId = 1,

    /// <summary>
    /// Количество меньше или равно нулю
    /// </summary>
    NegativeOrZeroQuantity = 2,

    /// <summary>
    /// Не указано направление
    /// </summary>
    UndefinedSide = 3,

    /// <summary>
    /// Цена ниже или равна нулю
    /// </summary>
    NegativeOrZeroPrice = 4,

    /// <summary>
    /// Цена не соответствует шагу цены
    /// </summary>
    PriceStepDecreased = 5,

    /// <summary>
    /// Недостаточно ликвидности
    /// </summary>
    NotEnoughLiquidity = 6,

    Exchange = 7
}
