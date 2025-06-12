using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;
using Banana.Backtest.Emulator.Services;
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
        services.AddStep<MarketDataStreamingStep>();
        services.AddStep<EmulatorStep>();
        services.AddHostedService(provider => provider.GetRequiredService<StepsChainExecutor>());
        services.AddSingleton<IChannelsProvider, BacktestChannelsProvider>();
        services.AddSingleton<IMarketDataChannelsProvider<TradeUpdate>, BacktestMarketDataChannelsProvider<TradeUpdate>>();
        services.AddSingleton<IMarketDataChannelsProvider<LevelUpdate>, BacktestMarketDataChannelsProvider<LevelUpdate>>();
        services.AddSingleton<IMarketDataChannelsProvider<OrderBookSnapshot>, BacktestMarketDataChannelsProvider<OrderBookSnapshot>>();
        services.AddSingleton<TimeProvider, BacktestClock>();
    }

    public static void ConfigureEmulator(this IServiceCollection services)
    {
        services.AddSingleton<BacktestMatcher>();
    }

    private static void AddStep<TStep>(this IServiceCollection services)
        where TStep : class, IBacktestStep
    {
        services.AddScoped<IBacktestStep, TStep>();
    }
}
