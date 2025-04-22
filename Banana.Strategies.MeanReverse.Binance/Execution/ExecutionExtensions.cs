using Banana.Strategies.MeanReverse.Binance.Endpoints;
using Banana.Strategies.MeanReverse.Binance.Execution.CombinedExecution;
using Banana.Strategies.MeanReverse.Binance.Execution.Fast;
using Banana.Strategies.MeanReverse.Binance.Execution.FrontRun;
using Banana.Strategies.MeanReverse.Binance.Persistence;
using Banana.Strategies.MeanReverse.Binance.UserData.RiskManagement;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;

namespace Banana.Strategies.MeanReverse.Binance.Execution;

public static class ExecutionExtensions
{
    public static void AddExecutions(this IServiceCollection services, IConfiguration configuration)
    {
        var executionOptions = configuration.GetSection(nameof(ExecutionOptions)).Get<ExecutionOptions>();
        ArgumentNullException.ThrowIfNull(executionOptions);
        services.Configure<ExecutionOptions>(configuration.GetSection(nameof(ExecutionOptions)));
        services.TryAddScoped<RiskManager>();
        services.TryAddSingleton<DeferredExecution>();
        services.TryAddSingleton<DeferredRiskManagement>();
        services.TryAddScoped<IExecution<FastExecutionLaunchCommand>, FastExecution>();
        services.TryAddScoped<IExecution<FrontRunExecutionLaunchCommand>, FrontRunExecution>();
        services.TryAddScoped<IExecution<CombinedExecutionCommand<FastExecutionLaunchCommand>>, CombinedExecution<FastExecutionLaunchCommand>>();
        services.TryAddScoped<IExecution<CombinedExecutionCommand<FrontRunExecutionLaunchCommand>>, CombinedExecution<FrontRunExecutionLaunchCommand>>();
        if (executionOptions.SaveToDatabase)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionOptions.MongoDbConnectionString);
            services.AddExecutionDataSaver(executionOptions.MongoDbConnectionString);
        }
    }

    private static void AddExecutionDataSaver(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
        services.AddSingleton<MongoDbContext>();
        services.AddHostedService<UserDataSaverService>();
    }
}
