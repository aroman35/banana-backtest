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
using Elastic.CommonSchema.Serilog;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using FastEndpoints;
using FastEndpoints.Swagger;
using MongoDB.Driver;
using OpenTelemetry.Metrics;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<DataFlowMediator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(builder.Configuration.GetConnectionString("Mongo")));
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddMemoryCache();
builder.Services.Configure<RuntimeSettings>(builder.Configuration.GetSection(nameof(RuntimeSettings)));
builder.Services.Configure<StrategySettings>(builder.Configuration.GetSection(nameof(StrategySettings)));
builder.Services.Configure<HostOptions>(options =>
{
    options.ServicesStartConcurrently = true;
    options.ServicesStopConcurrently = true;
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
    options.ShutdownTimeout = TimeSpan.FromSeconds(180);
    // options.StartupTimeout = TimeSpan.FromSeconds(300);
});

builder.Services.AddHostedService<UserStreamsLauncher>();
builder.Services.AddHostedService<TradesStreamLauncher>();
builder.Services.AddHostedService<UserDataSaverService>();

builder.Services.AddCache<InstrumentsCache, BinanceFuturesSymbol>();
builder.Services.AddCache<UserPositionsCache, UserPosition>();
builder.Services.AddCache<UserBalanceCache, BinanceFuturesStreamBalance>();
builder.Services.AddCache<OrderBooksCache, ISymbolOrderBook>();
builder.Services.AddHostedService<TestingStrategyService>();
builder.Services.AddExecutions(builder.Configuration);

builder.Services.AddBinance(builder.Configuration.GetSection(nameof(BinanceSettings)));
builder.Services.AddSerilog(logger =>
    logger
        .ReadFrom.Configuration(builder.Configuration)
        .WriteTo.Elasticsearch(
            [new Uri(builder.Configuration.GetConnectionString("Elasticsearch") ?? throw new NullReferenceException())],
            options =>
            {
                options.BootstrapMethod = BootstrapMethod.Failure;
                options.DataStream = new DataStreamName("logs", "binance", "Crypto");
                options.MinimumLevel = LogEventLevel.Debug;
                options.TextFormatting = new EcsTextFormatterConfiguration
                {
                    IncludeActivityData = true,
                    IncludeHost = true,
                    IncludeProcess = true,
                    IncludeUser = true
                };
            }));

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
