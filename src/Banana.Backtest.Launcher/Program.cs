using Banana.Backtest.Launcher;
using Banana.Backtest.Launcher.Cache.Catalog;
using Banana.Backtest.Launcher.Extensions;
using Microsoft.Extensions.Options;
using Serilog;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
var pg = builder.Configuration.GetConnectionString("PostgreSQL");
Environment.SetEnvironmentVariable("PG_CONNECTION_STRING", pg);
builder.Services.ConfigureOptions<TinkoffOptions>(builder.Configuration);
builder.Services.ConfigureOptions<RedisOptions>(builder.Configuration);
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration.GetOptions<RedisOptions>().ToConfigurationOptions()));
builder.Services.AddSingleton<IDatabase>(provider =>
    provider
        .GetRequiredService<IConnectionMultiplexer>()
        .GetDatabase(provider.GetRequiredService<IOptions<RedisOptions>>().Value.CatalogDatabase));
builder.Services.AddInvestApiClient((provider, options) =>
{
    options.Sandbox = false;
    options.AccessToken = provider.GetRequiredService<IOptions<TinkoffOptions>>().Value.Token;
    options.AppName = "Banana";
});
builder.Services.AddSingleton<InstrumentsCatalog>();
builder.Services.AddHostedService<DataPreparation>();
// builder.Services.AddHostedService<ModelTrainerService>();
// builder.Services.AddHostedService<CatalogLoader>();
builder.Services.AddSerilog((_, loggerConfiguration) => loggerConfiguration.ReadFrom.Configuration(builder.Configuration));

var host = builder.Build();
host.Run();
