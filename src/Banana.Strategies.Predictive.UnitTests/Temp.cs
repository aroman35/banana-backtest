using System.IO.Compression;
using System.Reflection;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.Common.Services;
using Serilog;
using Xunit.Abstractions;

namespace Banana.Strategies.Predictive.UnitTests;

public class Temp(ITestOutputHelper testOutputHelper)
{
    private readonly ILogger _logger = new LoggerConfiguration().WriteTo.TestOutput(testOutputHelper).CreateLogger();

    [Fact]
    public void Test1()
    {
        var symbol = Symbol.Create(Asset.Get("SiM4"), Asset.SPBFUT, Exchange.MoexFutures);
        var hash = MarketDataHash.Create(symbol, new DateOnly(2024, 04, 01));
        var settings = new LevelUpdatesConvertorSettings
        {
            StoragePath = @"D:\market-data-storage\",
            OutputDirectoryPath = @"E:\market-data\test-3",
            CompressionLevel = CompressionLevel.NoCompression,
            CompressionType = CompressionType.NoCompression,
            Hash = hash
        };
        using var levelUpdatesConverter = new LevelUpdatesConvertor(settings, _logger);
        levelUpdatesConverter.Start();
    }

    [Fact]
    public void Test2()
    {
        var sourcesDirectoryPath = @"E:\market-data\test-3";
        var symbol = Symbol.Create(Asset.Get("SiM4"), Asset.SPBFUT, Exchange.MoexFutures);
        var hash = MarketDataHash.Create(symbol, new DateOnly(2024, 04, 01));

        var levelUpdatesReader = MarketDataCacheAccessorProvider.CreateReader<LevelUpdate>(sourcesDirectoryPath, hash.For(FeedType.LevelUpdates));
        var tradesReader = MarketDataCacheAccessorProvider.CreateReader<TradeUpdate>(sourcesDirectoryPath, hash.For(FeedType.Trades));
        var allLevels = levelUpdatesReader.ContinueReadUntil().ToArray();
        var allTrades = tradesReader.ContinueReadUntil().ToArray();
    }

    private static void WriteToCsv<T>(IEnumerable<T> data, string filePath)
    {
        if (data == null)
            throw new ArgumentException("Data cannot be null or empty.");

        var type = typeof(T);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        if (properties.Length == 0)
            throw new ArgumentException("The class does not have any public properties to write.");

        using (var writer = new StreamWriter(filePath))
        {
            // Write the header row
            writer.WriteLine(string.Join(",", properties.Select(p => p.Name)));

            // Write the data rows
            foreach (var item in data)
            {
                var values = properties.Select(p =>
                {
                    var value = p.GetValue(item, null);
                    return value != null ? value.ToString().Replace(",", "\\,") : "";
                });

                writer.WriteLine(string.Join(",", values));
            }
        }
    }
}

public class FlatLevelUpdate
{
    public FlatLevelUpdate()
    {
    }

    public FlatLevelUpdate(MarketDataItem<LevelUpdate> levelUpdate)
    {
        Timestamp = levelUpdate.Timestamp;
        Price = levelUpdate.Item.Price;
        Quantity = levelUpdate.Item.Quantity;
        IsBid = levelUpdate.Item.IsBid;
    }


    public long Timestamp { get; set; }
    public double Price { get; set; }
    public double Quantity { get; set; }
    public bool IsBid { get; set; }
}

public class FlatTradeUpdate
{
    public FlatTradeUpdate()
    {
    }

    public FlatTradeUpdate(MarketDataItem<TradeUpdate> tradeUpdate)
    {
        Timestamp = tradeUpdate.Timestamp;
        IsBuy = tradeUpdate.Item.Side is Side.Long;
        Price = tradeUpdate.Item.Price;
        Quantity = tradeUpdate.Item.Quantity;
    }
    public long Timestamp { get; set; }
    public bool IsBuy { get; set; }
    public double Price { get; set; }
    public double Quantity { get; set; }
}
