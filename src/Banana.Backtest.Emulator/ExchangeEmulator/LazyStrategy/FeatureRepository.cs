using Npgsql;
using NpgsqlTypes;
using Serilog;

namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy;

/// <summary>
    /// FeatureRepository предоставляет оптимизированные методы для работы с целевыми фичами,
    /// включая массовую вставку, удаление и обновление данных в PostgreSQL.
    /// </summary>
    public class FeatureRepository
    {
        private readonly string _connectionString;
        private readonly string _tableName;
        private readonly string _ticker;
        private readonly ILogger _logger;

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="FeatureRepository"/>
        /// </summary>
        /// <param name="connectionString">
        /// Строка подключения к PostgreSQL
        /// </param>
        /// <param name="logger">
        /// Экземпляр <see cref="ILogger"/> для логирования
        /// </param>
        public FeatureRepository(
            string connectionString,
            string tableName,
            string ticker,
            ILogger logger)
        {
            _connectionString = connectionString;
            _tableName = tableName;
            _ticker = ticker;
            _logger = logger.ForContext<FeatureRepository>();
            _logger.Information("FeatureRepository initialized");
        }

        /// <summary>
        /// Массово вставляет список целевых фич в указанную таблицу с использованием команды COPY
        /// для оптимизации процесса вставки большого объёма данных.
        /// </summary>
        /// <param name="features">
        /// Список целевых фич (<see cref="FeatureRecordMapped"/>)
        /// </param>
        /// <param name="tableName">
        /// Имя таблицы для вставки данных
        /// </param>
        public void BulkInsertFeatures(List<FeatureRecordMapped> features, string? ticker = null)
        {
            _logger.Information("Starting bulk insert of {Count} features into table {Table}",
                features.Count,
                _tableName);

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var writer = connection.BeginBinaryImport(
                    $"COPY {_tableName} (ticker, timestamp, bestbid, bestask, spread, imbalance, vwapbid, vwapask, totaltradevolume, tradecount, buytradevolume, selltradevolume, tradepricechange, midprice, spreadpct, pricechangepct, tradeintensity, label) FROM STDIN (FORMAT BINARY)"))
                {
                    foreach (var feature in features)
                    {
                        writer.StartRow();
                        writer.Write(ticker ?? _ticker, NpgsqlDbType.Varchar);
                        writer.Write(feature.Timestamp, NpgsqlDbType.Timestamp);
                        writer.Write(feature.BestBid, NpgsqlDbType.Real);
                        writer.Write(feature.BestAsk, NpgsqlDbType.Real);
                        writer.Write(feature.Spread, NpgsqlDbType.Real);
                        writer.Write(feature.Imbalance, NpgsqlDbType.Real);
                        writer.Write(feature.VWAPBid, NpgsqlDbType.Real);
                        writer.Write(feature.VWAPAsk, NpgsqlDbType.Real);
                        writer.Write(feature.TotalTradeVolume, NpgsqlDbType.Real);
                        writer.Write(feature.TradeCount, NpgsqlDbType.Real);
                        writer.Write(feature.BuyTradeVolume, NpgsqlDbType.Real);
                        writer.Write(feature.SellTradeVolume, NpgsqlDbType.Real);
                        writer.Write(feature.TradePriceChange, NpgsqlDbType.Real);
                        writer.Write(feature.MidPrice, NpgsqlDbType.Real);
                        writer.Write(feature.SpreadPct, NpgsqlDbType.Real);
                        writer.Write(feature.PriceChangePct, NpgsqlDbType.Real);
                        writer.Write(feature.TradeIntensity, NpgsqlDbType.Real);
                        writer.Write(feature.Label, NpgsqlDbType.Real);
                    }
                    writer.Complete();
                }
            }

            _logger.Information("Bulk insert complete");
        }

        /// <summary>
        /// Удаляет фичи для заданного тикера и диапазона дат из указанной таблицы
        /// с использованием одного SQL‑запроса для оптимизации.
        /// </summary>
        /// <param name="tableName">
        /// Имя таблицы
        /// </param>
        /// <param name="ticker">
        /// Тикер инструмента
        /// </param>
        /// <param name="startDate">
        /// Начальная дата диапазона
        /// </param>
        /// <param name="endDate">
        /// Конечная дата диапазона
        /// </param>
        public void DeleteFeatures(string ticker)
        {
            _logger.Information(
                "Deleting features for ticker {Ticker} from table {Table}",
                ticker,
                _tableName);

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand(
                    @$"DELETE FROM {_tableName}", connection))
                {
                    command.Parameters.AddWithValue("@ticker", ticker);
                    int rowsAffected = command.ExecuteNonQuery();
                    _logger.Information("Deleted {Rows} rows", rowsAffected);
                }
            }
        }

        /// <summary>
        /// Обновляет фичи для заданного тикера и диапазона дат в указанной таблице.
        /// Для обновления сначала удаляются старые записи, затем выполняется массовая вставка новых данных.
        /// </summary>
        /// <param name="features">
        /// Список обновленных фич (<see cref="FeatureRecordMapped"/>)
        /// </param>
        /// <param name="tableName">
        /// Имя таблицы
        /// </param>
        /// <param name="ticker">
        /// Тикер инструмента
        /// </param>
        /// <param name="startDate">
        /// Начальная дата диапазона
        /// </param>
        /// <param name="endDate">
        /// Конечная дата диапазона
        /// </param>
        public void UpdateFeatures(
            List<FeatureRecordMapped> features,
            string ticker)
        {
            _logger.Information(
                "Updating features for ticker {Ticker} between {StartDate} and {EndDate} in table {Table}",
                ticker,
                _tableName);

            // Удаляем старые записи
            // DeleteFeatures(ticker);

            // Выполняем массовую вставку новых данных
            foreach (var featureRecordMappedse in features.Chunk(100_000))
            {
                BulkInsertFeatures(featureRecordMappedse.ToList(), ticker);
            }

            _logger.Information("Update complete for ticker {Ticker}", ticker);
        }
    }
