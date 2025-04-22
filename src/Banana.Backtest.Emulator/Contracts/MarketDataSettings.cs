using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.Emulator.Contracts;

/// <summary>
/// Настройки чтения рыночных данных
/// </summary>
public class MarketDataSettings
{
    /// <summary>
    /// Тикер (для крипты base asset)
    /// </summary>
    public required string Ticker { get; set; }

    /// <summary>
    /// Площадка (для крипты - quote asset)
    /// </summary>
    public required string ClassCode { get; set; }

    /// <summary>
    /// Биржа
    /// </summary>
    public Exchange Exchange { get; set; }

    /// <summary>
    /// Директория с рыночными данными
    /// </summary>
    public required string MarketDataDirectory { get; set; }

    /// <summary>
    /// Дата торгов
    /// </summary>
    public DateOnly TradeDate { get; set; }
}
