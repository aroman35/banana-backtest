using System.Globalization;
using System.Text.Json.Serialization;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Options;
using Banana.Backtest.CryptoConverter.Converters;
using Banana.Backtest.CryptoConverter.Endpoints.ExchangeInfoRequest;
using Banana.Backtest.CryptoConverter.Extensions;
using Banana.Backtest.CryptoConverter.Options;
using Banana.Backtest.CryptoConverter.Parsers;
using Banana.Backtest.CryptoConverter.Scheduler.Jobs;
using Banana.Backtest.CryptoConverter.Services;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddServiceConfiguration("appsettings", builder.Configuration);
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

builder.Services
    .AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddPrometheusExporter()
        .AddHttpClientInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddMeter("converter"))
    .WithTracing(tracing =>
    {
        tracing.SetResourceBuilder(ResourceBuilder.CreateDefault());
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();
        tracing.AddHangfireInstrumentation();
        tracing.AddRedisInstrumentation();
    });
builder.Services.ConfigureOptions<TardisHttpClientOptions>(builder.Configuration);
builder.Services.ConfigureOptions<ConverterOptions>(builder.Configuration);
builder.Services.ConfigureOptions<MarketDataParserOptions>(builder.Configuration);
builder.Services.ConfigureOptions<RedisOptions>(builder.Configuration);

builder.Services
    .AddHttpClient<TardisClient>()
    .ConfigureHttpClient(builder.Configuration.GetOptions<TardisHttpClientOptions>());

builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
    ConnectionMultiplexer.Connect(
        provider.GetRequiredService<IOptions<RedisOptions>>().Value.ToConfigurationOptions()));

builder.Services.ConfigureHangfire(builder.Configuration);

builder.Services.AddSingleton<ParsersProvider>();
builder.Services.AddSingleton<CatalogRepository>();
builder.Services.AddScoped<RefreshInstrumentsJob>();
builder.Services.AddScoped<ExchangeConverterJob>();
builder.Services.AddScoped<MarketDataConverterJob<LevelUpdate>>();
builder.Services.AddScoped<MarketDataConverterJob<TradeUpdate>>();
builder.Services.AddSingleton<ILineParser<LevelUpdate>, LevelUpdateLineParser>();
builder.Services.AddSingleton<ILineParser<TradeUpdate>, TradeUpdateLineParser>();
builder.Services
    .AddFastEndpoints()
    .SwaggerDocument(swagger =>
    {
        swagger.NewtonsoftSettings = newtonsoft => newtonsoft.Converters.Add(SymbolNewtonsoftJsonConverter.Instance);
        swagger.SerializerSettings = serializer =>
        {
            serializer.Converters.Add(SymbolSystemTextJsonConverter.Instance);
            serializer.Converters.Add(new JsonStringEnumConverter());
        };
    });
builder.Services.AddHealthChecks()
    .AddCheck("Liveness", _ => HealthCheckResult.Healthy(), ["live"])
    .AddCheck("Readiness", _ => HealthCheckResult.Healthy());

builder.Host.UseSerilog((hostContext, loggerBuilder) => loggerBuilder
    .ReadFrom.Configuration(hostContext.Configuration));

await using var app = builder.Build();
app.UseHangfireDashboard();
app.UseFastEndpoints(config =>
    {
        config.Serializer.Options.Converters.Add(SymbolSystemTextJsonConverter.Instance);
        config.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
    })
    .UseSwaggerGen()
    .UseSwaggerUi();
app.MapPrometheusScrapingEndpoint();
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("ready")
});
app.MapHealthChecks("/healthz/live", new HealthCheckOptions
{
    Predicate = _ => false,
    AllowCachingResponses = true,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});
await app.RunAsync();