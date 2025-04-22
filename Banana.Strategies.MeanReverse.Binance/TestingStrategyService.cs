using System.Collections.Concurrent;
using Banana.Strategies.MeanReverse.Binance.DataFlow;
using Banana.Strategies.MeanReverse.Binance.Execution;
using Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;
using Banana.Strategies.MeanReverse.Binance.UserData.RiskManagement;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Objects.Models.Futures;
using Microsoft.Extensions.Options;

namespace Banana.Strategies.MeanReverse.Binance;

public class TestingStrategyService(
    IBinanceRestClient binanceClient,
    ICacheForSymbol<BinanceFuturesSymbol> instrumentsCache,
    DeferredExecution deferredExecution,
    DeferredRiskManagement deferredRiskManagement,
    IOptions<StrategySettings> strategySettings,
    IOptions<RuntimeSettings> runtimeSettings,
    DataFlowMediator mediator,
    ILogger logger) : BackgroundService
{
    private readonly ILogger _logger = logger.ForContext<TestingStrategyService>();
    private readonly ConcurrentDictionary<string, TestingStrategy> _strategies = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await instrumentsCache.WaitForInitialisation;
        var tickersResponse = await binanceClient.UsdFuturesApi.ExchangeData.GetTickersAsync(stoppingToken);

        var tickers = tickersResponse.Data.Where(x => x.Symbol.EndsWith("USDT")).ToArray();
        var volumes = tickers.Select(x => decimal.ToDouble(x.QuoteVolume)).ToArray();
        var lowerBoundVolumeQuantile = (decimal)MathNet.Numerics.Statistics.Statistics.Quantile(
            volumes,
            strategySettings.Value.LowerBoundVolumeQuantile);
        var upperBoundVolumeQuantile = (decimal)MathNet.Numerics.Statistics.Statistics.Quantile(
            volumes,
            strategySettings.Value.UpperBoundVolumeQuantile);

        var instruments = tickers.Where(x => x.QuoteVolume >= lowerBoundVolumeQuantile && x.QuoteVolume <= upperBoundVolumeQuantile).ToArray();
        var priceChanges = instruments.Select(x => decimal.ToDouble(x.PriceChangePercent)).ToArray();

        var lowerBoundVolatilityQuantile = (decimal)MathNet.Numerics.Statistics.Statistics.Quantile(
            priceChanges,
            strategySettings.Value.LowerBoundVolatilityQuantile);
        var upperBoundVolatilityQuantile = (decimal)MathNet.Numerics.Statistics.Statistics.Quantile(
            priceChanges,
            strategySettings.Value.UpperBoundVolatilityQuantile);

        var symbols = instruments
            .Where(x => x.PriceChangePercent >= lowerBoundVolatilityQuantile &&
                        x.PriceChangePercent <= upperBoundVolatilityQuantile)
            .OrderByDescending(x => x.PriceChange)
            .Select(x => x.Symbol)
            .ToHashSet();
        _logger.Information("Prepared {Count} instruments", symbols.Count);
        foreach (var symbol in runtimeSettings.Value.LimitedSymbols ?? symbols)
        {
            var instrument = instrumentsCache.Get(symbol);
            var strategy = new TestingStrategy(
                deferredExecution,
                deferredRiskManagement,
                mediator,
                strategySettings,
                logger);
            _ = Task.Run(() => strategy.ExecuteAsync(instrument, stoppingToken), stoppingToken);
            _strategies.TryAdd(symbol, strategy);
        }
    }
}
