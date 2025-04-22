using System.Diagnostics;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.Common.Services;
using Xunit.Abstractions;

namespace Banana.Strategies.Predictive.UnitTests;

public class MmfTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void Test1()
    {
        var started = Stopwatch.StartNew();
        var symbol = Symbol.Parse("NGK4", "SPBFUT", Exchange.MoexFutures);
        var hash = MarketDataHash.Create(symbol, new DateOnly(2024, 04, 29), FeedType.Trades);
        var reader = new MarketDataCacheReaderMmf<TradeUpdate>("d:/market-data-storage", hash);
        var arr = reader.ContinueReadUntil().ToArray();
        // var reader = MarketDataCacheAccessorProvider.CreateReader<TradeUpdate>("d:/market-data-storage", hash);
        // foreach (var item in reader.ContinueReadUntil())
        // {
        //     testOutputHelper.WriteLine(item.ToString());
        // }
        var elapsed = started.Elapsed;
        testOutputHelper.WriteLine($"Elapsed: {elapsed} ms");
    }
}
