using Banana.Backtest.Launcher.Extensions;
using Banana.Backtest.Launcher.Infrastructure;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.ConfigureBacktestOptions(builder.Configuration);
builder.Services.ConfigureApplicationInfrastructure();
builder.Services.ConfigureEmulator();
builder.ConfigureContainer(new ServiceProviderFactory());
builder.Services.AddSerilog((_, loggerConfiguration) => loggerConfiguration.ReadFrom.Configuration(builder.Configuration));

using var host = builder.Build();
try
{
    await host.RunAsync();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
