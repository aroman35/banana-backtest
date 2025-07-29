using System.Text.Json.Serialization;
using Banana.Strategies.MeanReverse.Binance;
using Banana.Strategies.MeanReverse.Binance.DataFlow;
using Banana.Strategies.MeanReverse.Binance.Execution;
using Banana.Strategies.MeanReverse.Binance.Extensions;
using Banana.Strategies.MeanReverse.Binance.Extensions.Launchers;
using Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;
using Banana.Strategies.MeanReverse.Binance.Persistence;
using Banana.Strategies.MeanReverse.Binance.UserData.Positions;
using Binance.Net.Objects.Models.Futures;
using Binance.Net.Objects.Models.Futures.Socket;
using CryptoExchange.Net.Interfaces;
using FastEndpoints;
using FastEndpoints.Swagger;
using OpenTelemetry.Metrics;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<DataFlowMediator>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddMemoryCache();
builder.Services.Configure<RuntimeSettings>(builder.Configuration.GetSection(nameof(RuntimeSettings)));
builder.Services.Configure<StrategySettings>(builder.Configuration.GetSection(nameof(StrategySettings)));
builder.Services.Configure<HostOptions>(options =>
{
    options.ServicesStartConcurrently = true;
    options.ServicesStopConcurrently = true;
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
    options.ShutdownTimeout = TimeSpan.FromSeconds(180);
});

builder.Services.AddScoped<ReportsProvider>();
builder.Services.AddHostedService<ReportsProviderService>();
builder.Services.AddHostedService<UserStreamsLauncher>();
builder.Services.AddHostedService<TradesStreamLauncher>();

builder.Services.AddCache<InstrumentsCache, BinanceFuturesSymbol>();
builder.Services.AddCache<UserPositionsCache, UserPosition>();
builder.Services.AddCache<UserBalanceCache, BinanceFuturesStreamBalance>();
builder.Services.AddCache<OrderBooksCache, ISymbolOrderBook>();
builder.Services.AddHostedService<TestingStrategyService>();
builder.Services.AddExecutions(builder.Configuration);

builder.Services.AddBinance(builder.Configuration.GetSection(nameof(BinanceSettings)));
builder.Services.AddSerilog(logger => logger.ReadFrom.Configuration(builder.Configuration));

builder.Services
    .AddFastEndpoints()
    .SwaggerDocument(swagger =>
    {
        swagger.SerializerSettings = serializer =>
        {
            serializer.Converters.Add(new JsonStringEnumConverter());
        };
    });
builder.Services
    .AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddHttpClientInstrumentation();
        metrics.AddPrometheusExporter();
    });

await builder.Services.AddChannels();

var app = builder.Build();
app.MapPrometheusScrapingEndpoint();
app.UseFastEndpoints(config =>
    {
        config.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
    })
    .UseSwaggerGen()
    .UseSwaggerUi();

app.Run();
