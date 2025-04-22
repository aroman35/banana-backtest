namespace Banana.Backtest.Emulator.Contracts;

/// <summary>
/// Настройки для матчера
/// </summary>
public class MatcherSettings
{
    /// <summary>
    /// Минимальный шаг цены
    /// </summary>
    public double PriceStep { get; set; }

    /// <summary>
    /// Стоимость пункта цены
    /// </summary>
    public double PointPrice { get; set; }

    /// <summary>
    /// Гарантийное обеспечение для длинной позиции
    /// </summary>
    public double WarrantyCoverageLong { get; set; }

    /// <summary>
    /// Гарантийное обеспечение для короткой позиции
    /// </summary>
    public double WarrantyCoverageShort { get; set; }

    /// <summary>
    /// Комиссия для мейкер-заявок
    /// </summary>
    public double MakerFee { get; set; }

    /// <summary>
    /// Комиссия для тейкер-заявок
    /// </summary>
    public double TakerFee { get; set; }
}
