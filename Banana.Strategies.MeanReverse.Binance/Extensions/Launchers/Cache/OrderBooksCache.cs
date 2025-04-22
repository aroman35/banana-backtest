using System.Runtime.CompilerServices;
using Banana.Strategies.MeanReverse.Binance.DataFlow;
using Banana.Strategies.MeanReverse.Binance.MarketData;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Models.Futures;
using CryptoExchange.Net.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using static Banana.Strategies.MeanReverse.Binance.Extensions.JobsExtensions;

namespace Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;

public class OrderBooksCache(
    ICacheForSymbol<BinanceFuturesSymbol> instrumentsCache,
    IMemoryCache memoryCache,
    IBinanceOrderBookFactory orderBookFactory,
    IOptions<RuntimeSettings> runtimeSettings,
    DataFlowMediator mediator,
    ILogger logger) : CacheForSymbol<ISymbolOrderBook>(memoryCache, logger: logger)
{
    private readonly ILogger _logger = logger;

    protected override async IAsyncEnumerable<KeyValuePair<string, ISymbolOrderBook>> LoadData(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await instrumentsCache.WaitForInitialisation;
        var orderBooks = new List<ISymbolOrderBook>();
        var symbols = runtimeSettings.Value.LimitedSymbols ?? instrumentsCache.Symbols;
        foreach (var symbol in symbols)
        {
            var orderBook = orderBookFactory.UsdFutures.Create(
                symbol,
                options =>
                {
                    options.Limit = 20;
                    options.ChecksumValidationEnabled = true;
                    options.InitialDataTimeout = TimeSpan.FromSeconds(5);
                });

            orderBook.OnBestOffersChanged += bestOffer =>
            {
                var orderBookTop = new OrderBookTop(
                    new OrderBookEntry(bestOffer.BestBid.Price, bestOffer.BestBid.Quantity),
                    new OrderBookEntry(bestOffer.BestAsk.Price, bestOffer.BestAsk.Quantity));
                SafeExecute(top => mediator.SendForSymbol(top, symbol), orderBookTop, _logger);
            };
            orderBook.OnStatusChange += (oldStatus, newStatus) => _logger.Debug(
                "[{Symbol}] order-book status changed: {OldStatus} -> {Status}]",
                orderBook.Symbol,
                oldStatus,
                newStatus);
            yield return new KeyValuePair<string, ISymbolOrderBook>(symbol, orderBook);
            orderBooks.Add(orderBook);
        }

        // var counter = 0;
        // foreach (var connectionTask in connectionTasks)
        // {
        //     await connectionTask;
        //     if (++counter % 8 == 0)
        //         await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        // }
        foreach (var tasksToDeploy in orderBooks.Chunk(10))
        {
            await Task.WhenAll(tasksToDeploy.Select(x => x.StartAsync(cancellationToken)));
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }
}
