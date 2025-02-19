using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models.MarketData;
using Npgsql;
using Serilog;

namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy;

/// <summary>
/// TradeRepository предоставляет методы для массовой вставки сделок в PostgreSQL.
/// </summary>
public class TradeRepository
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TradeRepository"/>
    /// </summary>
    /// <param name="connectionString">
    /// Строка подключения к PostgreSQL
    /// </param>
    /// <param name="logger">
    /// Экземпляр <see cref="ILogger"/> для логирования
    /// </param>
    public TradeRepository(
        string connectionString,
        ILogger logger)
    {
        _connectionString = connectionString;
        _logger = logger.ForContext<TradeRepository>();
        _logger.Information("TradeRepository initialized");
    }

    /// <summary>
    /// Выполняет массовую вставку списка сделок в указанную таблицу с использованием команды COPY
    /// для оптимизации процесса вставки большого объёма данных.
    /// </summary>
    /// <param name="trades">
    /// Список сделок (<see cref="TradeUpdate"/>)
    /// </param>
    /// <param name="tableName">
    /// Имя таблицы для вставки данных
    /// </param>
    /// <param name="ticker">
    /// Тикер инструмента
    /// </param>
    public void BulkInsertTrades(
        List<MarketDataItem<TradeUpdate>> trades,
        string tableName,
        string ticker)
    {
        _logger.Information(
            "Starting bulk insert of {Count} trades into table {Table}",
            trades.Count,
            tableName);

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            connection.Open();
            using (var writer = connection.BeginBinaryImport(
                       $"COPY {tableName} (ticker, side, price, quantity, tradedate) FROM STDIN (FORMAT BINARY)"))
            {
                foreach (var trade in trades)
                {
                    writer.StartRow();
                    writer.Write(ticker, NpgsqlTypes.NpgsqlDbType.Varchar);
                    writer.Write((short)trade.Item.Side, NpgsqlTypes.NpgsqlDbType.Smallint);
                    writer.Write(trade.Item.Price, NpgsqlTypes.NpgsqlDbType.Double);
                    writer.Write(trade.Item.Quantity, NpgsqlTypes.NpgsqlDbType.Double);
                    writer.Write(trade.Timestamp.AsDateTime(), NpgsqlTypes.NpgsqlDbType.TimestampTz);
                    // Поле tradedate заполнится автоматически, если его не указывать
                }

                writer.Complete();
            }
        }

        _logger.Information("Bulk insert of trades complete");
    }
}
