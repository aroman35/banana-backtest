using Banana.Backtest.Launcher.Extensions;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.ConfigureBacktestOptions(builder.Configuration);
builder.Services.ConfigureApplicationInfrastructure();
builder.Services.ConfigureEmulator();
builder.Services.AddSerilog((_, loggerConfiguration) => loggerConfiguration.ReadFrom.Configuration(builder.Configuration));

var host = builder.Build();
host.Run();
