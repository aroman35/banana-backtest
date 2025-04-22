namespace Banana.Backtest.Emulator.Contracts;

/// <summary>
/// Статус заявки
/// </summary>
[Flags]
public enum OrderStatus
{
    /// <summary>
    /// Отсутствующий статус
    /// </summary>
    Unspecified = 1 << 0,

    /// <summary>
    /// Исполнена
    /// </summary>
    Fill = 1 << 1,

    /// <summary>
    /// Отклонена
    /// </summary>
    Rejected = 1 << 2,

    /// <summary>
    /// Отменена пользователем
    /// </summary>
    Cancelled = 1 << 3,

    /// <summary>
    /// Новая
    /// </summary>
    New = 1 << 4,

    /// <summary>
    /// Частично исполнена
    /// </summary>
    PartiallyFill = 1 << 5,

    FinalState = Fill | Rejected | Cancelled
}
