using System.Reflection;
using Banana.Strategies.MeanReverse.Binance.Extensions;
using Binance.Net.Objects.Models.Futures.Socket;
using MongoDB.Driver;
using OfficeOpenXml;

namespace Banana.Strategies.MeanReverse.Binance.Persistence;

public class ReportsProvider(TimeProvider clock, MongoDbContext mongoDbContext, ILogger logger)
    : IAsyncDisposable, IDisposable
{
    static ReportsProvider()
    {
        ExcelPackage.License.SetNonCommercialPersonal("banana");
    }

    private readonly ILogger _logger = logger.ForContext<ReportsProvider>();
    private readonly Lock _lock = new Lock();

    private ExcelPackage? _package;
    private DateTime _reportDate;
    private Stream? _outputFile;

    private volatile bool _packageFlushed;

    public async Task SaveDailyReport(CancellationToken cancellationToken)
    {
        _reportDate = clock.TodayShift(TimeSpan.FromDays(1) * -1);
        _outputFile = CreateOutputFile(_reportDate, out var outputFileName);
        await GenerateReport(_outputFile, _reportDate, _reportDate.AddDays(1), cancellationToken);
        _logger.Information("Report saved: {ReportPath}", outputFileName);
    }

    public async Task GenerateCustomReport(Stream outputStream, DateTime reportStartDate, DateTime reportEndDate, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(reportStartDate, reportEndDate, nameof(reportEndDate));
        await GenerateReport(outputStream, reportStartDate, reportEndDate, cancellationToken);

    }

    private async Task GenerateReport(Stream outputStream, DateTime reportStartDate, DateTime reportEndDate, CancellationToken cancellationToken)
    {
        CheckFlushed();
        try
        {
            using (_lock.EnterScope())
            {
                CheckFlushed();
                var tempPackage = new ExcelPackage(outputStream);
                var originalValue = Interlocked.CompareExchange(ref _package, tempPackage, null);
                if (originalValue is not null)
                    throw new AccessViolationException("Attempt to use the same report for many times");
            }

            using var tradesPage = _package.Workbook.Worksheets.Add("trades");
            using var ordersPage = _package.Workbook.Worksheets.Add("orders");

            // f(t) | t ∈ [datetime(D,00:00:00),datetime(D+1,00:00:00))
            var allTrades = await mongoDbContext.UserTradesCollection
                .Find(Builders<BinanceFuturesStreamTradeUpdate>.Filter.And(
                    Builders<BinanceFuturesStreamTradeUpdate>.Filter.Gte(x => x.TransactionTime, reportStartDate),
                    Builders<BinanceFuturesStreamTradeUpdate>.Filter.Lt(x => x.TransactionTime, reportEndDate)))
                .ToListAsync(cancellationToken);

            var allOrders = await mongoDbContext.UserOrdersCollection
                .Find(Builders<BinanceFuturesStreamOrderUpdateData>.Filter.And(
                    Builders<BinanceFuturesStreamOrderUpdateData>.Filter.Gte(x => x.UpdateTime, reportStartDate),
                    Builders<BinanceFuturesStreamOrderUpdateData>.Filter.Lt(x => x.UpdateTime, reportEndDate)))
                .ToListAsync(cancellationToken);

            AddDataTable(tradesPage, allTrades, "trades_table");
            AddDataTable(ordersPage, allOrders, "orders_table");

            await _package.SaveAsync(cancellationToken);
            _packageFlushed = true;
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Failed to generate report");
            throw;
        }
    }

    private static void AddDataTable<T>(ExcelWorksheet page, ICollection<T> data, string? tableName = null)
    {
        var tableRange = page.Cells[1, 1].LoadFromCollection(data, true);
        var table = page.Tables.Add(tableRange, tableName ?? typeof(T).Name.ToLowerInvariant());
        table.ShowHeader = true;
        var printingProperties = typeof(T).GetProperties(BindingFlags.Instance|BindingFlags.Public);
        for (var i = 0; i < printingProperties.Length; i++)
        {
            if (printingProperties[i].PropertyType == typeof(DateTime))
            {
                table.Columns[i].DataStyle.NumberFormat.Format = "yyyy.MM.dd hh:mm:ss";
            }
        }
        tableRange.AutoFitColumns();
    }

    private void CheckFlushed()
    {
        if (_packageFlushed)
            throw new ObjectDisposedException("Excel package has been flushed. Consider to create a new instance of reports provider");
    }

    // TODO: вынести папку в сеттинг. Целевое решение - сетевая папка в облаке, но пока пох
    private FileStream CreateOutputFile(DateTime reportDate, out string reportFullName)
    {
        var fileName = $"report_binance_{reportDate:yyyy.MM.dd}.xlsx";
        var outputDirectoryPath = Path.Combine(Path.GetTempPath(), "binance_reports");

        if (!Directory.Exists(outputDirectoryPath))
            Directory.CreateDirectory(outputDirectoryPath);

        reportFullName =  Path.Combine(outputDirectoryPath, fileName);
        if (File.Exists(reportFullName))
            File.Delete(reportFullName);

        var fileStreamOptions = new FileStreamOptions
        {
            Access = FileAccess.ReadWrite,
            BufferSize = 1 << 15, // 32 Kb
            Share = FileShare.None,
            PreallocationSize = 1 << 13, // 8 Kb
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            Mode = FileMode.CreateNew
        };
        var fileStream = new FileStream(reportFullName, fileStreamOptions);
        return fileStream;
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        _package?.Dispose();
        if (_outputFile is not null)
            await _outputFile.DisposeAsync();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _package?.Dispose();
        _outputFile?.Dispose();
    }
}
