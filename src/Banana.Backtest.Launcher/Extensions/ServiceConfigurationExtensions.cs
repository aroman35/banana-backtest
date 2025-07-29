using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;
using Banana.Backtest.Emulator.Services;
using Banana.Backtest.Launcher.Infrastructure;
using Banana.Backtest.Launcher.Options;
using Banana.Backtest.Launcher.Steps;
using Banana.Backtest.Launcher.Steps.Common;
using FluentValidation;

namespace Banana.Backtest.Launcher.Extensions;

public static class ServiceConfigurationExtensions
{
    public static void ConfigureBacktestOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MarketDataSourcesOptions>(configuration.GetSection(nameof(MarketDataSourcesOptions)));
        services.Configure<StrategyOptions>(configuration.GetSection(nameof(StrategyOptions)));
        services.Configure<MatcherSettings>(configuration.GetSection(nameof(MatcherSettings)));
    }

    public static void ConfigureApplicationInfrastructure(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(Program).Assembly);
        services.AddSingleton<StepsChainExecutor>();
        services.AddStep<SettingsValidationBacktestStep>();
        services.AddStep<MarketDataPreparationBacktestStep>();
        services.AddStep<ClockLauncherStep>();
        services.AddStep<MatcherStep>();
        services.AddStep<MarketDataStreamingStep>();
        services.AddStep<StrategyStep>();
        services.AddHostedService(provider => provider.GetRequiredService<StepsChainExecutor>());
        services.AddSingleton<IChannelsProvider, BacktestChannelsProvider>();
        services.AddSingleton<IMarketDataChannelsProvider<TradeUpdate>, BacktestMarketDataChannelsProvider<TradeUpdate>>();
        services.AddSingleton<IMarketDataChannelsProvider<LevelUpdate>, BacktestMarketDataChannelsProvider<LevelUpdate>>();
        services.AddSingleton<IMarketDataChannelsProvider<OrderBookSnapshot>, BacktestMarketDataChannelsProvider<OrderBookSnapshot>>();
        services.AddSingleton<TimeProvider, BacktestClock>();
        services.AddStrategy<DummyStrategy>();
        services.AddScoped<BacktestAsyncServiceScope>();
    }

    public static void ConfigureEmulator(this IServiceCollection services)
    {
        services.AddScoped<BacktestMatcher>();
    }

    public static BacktestAsyncServiceScope CreateAsyncBacktestScope(this IServiceScopeFactory scopeFactory)
    {
        return scopeFactory.CreateScope().ServiceProvider.GetRequiredService<BacktestAsyncServiceScope>();
    }

    private static void AddStep<TStep>(this IServiceCollection services)
        where TStep : class, IBacktestStep
    {
        services.AddScoped<IBacktestStep, TStep>();
    }

    private static void AddStrategy<TStrategy>(this IServiceCollection services)
        where TStrategy : StrategyBase
    {
        services.AddScoped<StrategyBase, TStrategy>();
    }
}
