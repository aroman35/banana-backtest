using System.Reflection;
using Binance.Net.Objects.Models.Futures.Socket;
using MongoDB.Driver;
using OfficeOpenXml;

namespace Banana.Strategies.MeanReverse.Binance.Persistence;

/// <summary>
/// Сервис исключительно тестовый. Не включать в продакшн-сборку
/// </summary>
public class ReportsProvider(MongoDbContext mongoContext, TimeProvider clock, ILogger logger) : BackgroundService
{
    static ReportsProvider()
    {
        ExcelPackage.License.SetNonCommercialPersonal("banana");
    }

    private readonly ILogger _logger = logger.ForContext<ReportsProvider>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var outputFile = CreateOutputFile(out var reportFullName);
            using var package = new ExcelPackage(outputFile);
            using var tradesPage = package.Workbook.Worksheets.Add("trades");
            using var ordersPage = package.Workbook.Worksheets.Add("orders");
            var allTrades = await mongoContext.UserTradesCollection.Find(Builders<BinanceFuturesStreamTradeUpdate>.Filter.Empty).ToListAsync(stoppingToken);
            var allOrders = await mongoContext.UserOrdersCollection.Find(Builders<BinanceFuturesStreamOrderUpdateData>.Filter.Empty).ToListAsync(stoppingToken);
            AddDataTable(tradesPage, allTrades);
            AddDataTable(ordersPage, allOrders);
            await package.SaveAsync(stoppingToken);
            _logger.Information("Report saved: {ReportPath}", reportFullName);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Shit has happened during report creation");
            throw;
        }
    }

    private void AddDataTable<T>(ExcelWorksheet page, ICollection<T> data)
    {
        var tableRange = page.Cells[1, 1].LoadFromCollection(data, true);
        var table = page.Tables.Add(tableRange, typeof(T).Name.ToLowerInvariant());
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

    private Stream CreateOutputFile(out string reportFullName)
    {
        var fileName = $"report_binance_{clock.GetUtcNow().Date:yyyy.MM.dd}.xlsx";
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
}
