using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Launcher.Extensions;

// ReSharper disable ConvertToUsingDeclaration

namespace Banana.Backtest.Launcher.Steps.Common;

public class StepsChainExecutor(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime applicationLifetime,
    ILogger logger) : IHostedService
{
    private readonly ILogger _logger = logger.ForContext<StepsChainExecutor>();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using (var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            await using (var scope = scopeFactory.CreateAsyncBacktestScope())
            {
                var backgroundSteps = new List<Task>();
                var stepsSequence = scope
                    .GetRequiredService<IEnumerable<IBacktestStep>>()
                    .OrderBy(x => x.Priority);
                foreach (var step in stepsSequence)
                {
                    _logger.Information("Launching step [{Priority}]: {Name}", step.Priority,
                        Helpers.FriendlyTypeName(step.GetType()));
                    if (step.IsBackground)
                    {
                        var stepTask = LaunchBackgroundStep(step, cancellationTokenSource);
                        backgroundSteps.Add(stepTask);
                        continue;
                    }

                    try
                    {
                        await step.WaitForCompletion(cancellationTokenSource.Token);
                    }
                    catch (Exception exception)
                    {
                        _logger.Error(exception, "Step {Name} failed", Helpers.FriendlyTypeName(step.GetType()));
                        await cancellationTokenSource.CancelAsync();
                        break;
                    }
                    finally
                    {
                        _logger.Information(
                            "Step [{Priority}]: {Name} finished",
                            step.Priority,
                            Helpers.FriendlyTypeName(step.GetType()));
                    }
                }

                await Task.WhenAll(backgroundSteps);
                _logger.Information("Backtest finished");
            }
        }

        // applicationLifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task<Task> LaunchBackgroundStep(IBacktestStep backtestStep,
        CancellationTokenSource cancellationTokenSource)
    {
        var stepLauncher = Task.Factory.StartNew(async taskCancellationSource =>
            {
                var cts = (CancellationTokenSource)taskCancellationSource!;
                try
                {
                    await backtestStep.WaitForCompletion(cts.Token);
                }
                catch (Exception exception)
                {
                    _logger.Error(
                        exception,
                        "Background step [{Priority}] {StepName} failed",
                        backtestStep.Priority,
                        Helpers.FriendlyTypeName(backtestStep.GetType()));
                    await cts.CancelAsync();
                }
            },
            cancellationTokenSource,
            TaskCreationOptions.LongRunning);

        await backtestStep.IsInitialized;
        return await stepLauncher;
    }
}
