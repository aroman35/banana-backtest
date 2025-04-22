using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;
using Banana.Backtest.Emulator.Services;
using Banana.Backtest.Launcher.Extensions;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace Banana.Backtest.Launcher.Launchers;

public class BacktestHost
{
    private static Action<LoggerSinkConfiguration> _defaultLoggerConfiguration = sink => sink.Console(
        restrictedToMinimumLevel: LogEventLevel.Debug,
        outputTemplate:
        "{Timestamp:yyyy-MM-ddTHH:mm:ss.ffffffZ}|{Level:u3}|{Message:lj} [{SourceContext}]{NewLine}{Exception}");

    public static async Task CreateHost<TStrategy>(
        MatcherSettings matcherSettings,
        MarketDataSettings marketDataSettings,
        CancellationToken cancellationToken,
        Action<LoggerSinkConfiguration>? configureLogger = null)
        where TStrategy : StrategyBase
    {
        var serviceProvider = new ServiceCollection()
            .AddSerilog((context, logger) => logger.MinimumLevel
                .Debug().WriteTo
                .TimeWarp(
                    _ =>
                    {
                        try
                        {
                            return context.GetRequiredService<TimeProvider>().GetUtcNow();
                        }
                        catch (ObjectDisposedException)
                        {
                            return DateTimeOffset.UtcNow;
                        }
                    },
                    sink =>
                    {
                        if (configureLogger is null)
                        {
                            _defaultLoggerConfiguration(sink);
                            return;
                        }

                        configureLogger.Invoke(sink);
                    }))
            .AddSingleton<IChannelsProvider, BacktestChannelsProvider>()
            .AddSingleton<IMarketDataChannelsProvider<LevelUpdate>, BacktestMarketDataChannelsProvider<LevelUpdate>>()
            .AddSingleton<IMarketDataChannelsProvider<TradeUpdate>, BacktestMarketDataChannelsProvider<TradeUpdate>>()
            .AddSingleton<IMarketDataChannelsProvider<OrderBookSnapshot>, BacktestMarketDataChannelsProvider<OrderBookSnapshot>>()
            .AddSingleton<BacktestClockLauncher>()
            .AddSingleton<TimeProvider>(provider => provider.GetRequiredService<BacktestClockLauncher>())
            .AddSingleton<StrategyBase, TStrategy>()
            .AddOptions<MatcherSettings>()
            .Configure(options =>
            {
                options.PriceStep = matcherSettings.PriceStep;
                options.PointPrice = matcherSettings.PointPrice;
                options.WarrantyCoverageLong = matcherSettings.WarrantyCoverageLong;
                options.WarrantyCoverageShort = matcherSettings.WarrantyCoverageShort;
                options.MakerFee = matcherSettings.MakerFee;
                options.TakerFee = matcherSettings.TakerFee;
            })
            .Services
            .AddOptions<MarketDataSettings>()
            .Configure(options =>
            {
                options.Ticker = marketDataSettings.Ticker;
                options.ClassCode = marketDataSettings.ClassCode;
                options.Exchange = marketDataSettings.Exchange;
                options.MarketDataDirectory = marketDataSettings.MarketDataDirectory;
                options.TradeDate = marketDataSettings.TradeDate;
            })
            .Services
            .AddHostedService<PnlCalculatorLauncher>()
            .AddHostedService<StrategyLauncher>()
            .AddHostedService<BacktestMatcherLauncher>()
            .AddHostedService(provider => provider.GetRequiredService<BacktestClockLauncher>())
            .AddHostedService<MarketDataProviderLauncher>()
            .BuildServiceProvider();

        await using var scope = serviceProvider.CreateAsyncScope();
        var hostedServices = scope.ServiceProvider.GetServices<IHostedService>().ToList();
        await Task.WhenAll(hostedServices.Select(h => h.StartAsync(CancellationToken.None)));
        await Task.WhenAll(hostedServices.Select(h => h.StopAsync(CancellationToken.None)));
        await Parallel.ForEachAsync(hostedServices.OfType<IAsyncDisposable>(), cancellationToken, (service,_) => service.DisposeAsync());
    }
}
