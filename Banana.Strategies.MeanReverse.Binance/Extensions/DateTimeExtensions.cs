namespace Banana.Strategies.MeanReverse.Binance.Extensions;

public static class DateTimeExtensions
{
    /// <summary>
    /// Формирует начало текущего дня по UTC с установленным Kind
    /// </summary>
    /// <param name="clock">Провайдер времени</param>
    /// <param name="kind">Возможность указать локальный ТЗ (для тестирования)</param>
    /// <returns></returns>
    public static DateTime Today(this TimeProvider clock, DateTimeKind kind = DateTimeKind.Utc)
    {
        return DateTime.SpecifyKind(clock.GetUtcNow().DateTime, kind).Date;
    }

    /// <summary>
    /// Получить дату смещенную относительно начала текущего дня
    /// </summary>
    /// <param name="clock"></param>
    /// <param name="shift"></param>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static DateTime TodayShift(this TimeProvider clock, TimeSpan shift, DateTimeKind kind = DateTimeKind.Utc)
    {
        var today = clock.Today(kind);
        return shift == TimeSpan.Zero ? today : today.Add(shift);
    }
}
