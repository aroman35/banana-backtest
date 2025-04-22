using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Strategies.MeanReverse;
using Banana.Strategies.MeanReverse.Launchers;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<IChannelsProvider, LiveChannelsProvider>();
builder.Services.AddSingleton<IMarketDataChannelsProvider<TradeUpdate>, LiveMarketDataChannelsProvider<TradeUpdate>>();
builder.Services.AddSingleton<IMarketDataChannelsProvider<LevelUpdate>, LiveMarketDataChannelsProvider<LevelUpdate>>();
builder.Services.AddSingleton<TimeProvider>(_ => TimeProvider.System);
builder.Services.Configure<StrategySettings>(builder.Configuration.GetSection(nameof(StrategySettings)));
builder.Services.AddInvestApiClient((provider, settings) =>
{
    var options = builder.Configuration.GetSection(nameof(TinkoffSettings)).Get<TinkoffSettings>();
    ArgumentNullException.ThrowIfNull(options);
    settings.Sandbox = false;
    settings.AccessToken = options.Token;
    settings.AppName = "banana";
});
builder.Services.AddHostedService<MarketDataProviderLauncher>();
builder.Services.AddHostedService<AccountConnector>();
builder.Services.AddHostedService<MeanReverseStrategy>();
builder.Services.AddSerilog((_, loggerConfiguration) => loggerConfiguration.ReadFrom.Configuration(builder.Configuration));

var host = builder.Build();
host.Run();
