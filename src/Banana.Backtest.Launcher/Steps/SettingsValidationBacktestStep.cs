using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Launcher.Options;
using Banana.Backtest.Launcher.Steps.Common;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;

namespace Banana.Backtest.Launcher.Steps;

public class SettingsValidationBacktestStep(
    IOptions<StrategyOptions> strategyOptions,
    IOptions<MarketDataSourcesOptions> marketDataSourcesOptions,
    IServiceScopeFactory serviceScopeFactory,
    ILogger logger) : IBacktestStep
{
    private readonly ILogger _logger = logger.ForContext<SettingsValidationBacktestStep>();
    public int Priority => 0;
    public bool IsBackground => false;

    public Task WaitForCompletion(CancellationToken cancellationToken = default)
    {
        var failures = new List<ValidationFailure>();
        failures.AddRange(ValidateOptions(strategyOptions, serviceScopeFactory));
        failures.AddRange(ValidateOptions(marketDataSourcesOptions, serviceScopeFactory));

        if (failures.Count > 0)
        {
            foreach (var failure in failures)
            {
                _logger.Error("Validation failed for {Type}: {Failure}", failure.PropertyName, failure.ErrorMessage);
            }
            var exception = new ValidationException("Settings are not valid", failures);
            return Task.FromException(exception);
        }

        return Task.CompletedTask;
    }

    private IEnumerable<ValidationFailure> ValidateOptions<TOptions>(IOptions<TOptions> options, IServiceScopeFactory scopeFactory)
        where TOptions : class
    {
        using var scope = scopeFactory.CreateScope();
        var validators = scope.ServiceProvider.GetRequiredService<IEnumerable<IValidator<TOptions>>>();
        var failuresCount = 0;
        foreach (var validator in validators)
        {
            var result = validator.Validate(options.Value);
            if (!result.IsValid)
            {
                foreach (var failure in result.Errors)
                {
                    ++failuresCount;
                    yield return failure;
                }
                _logger.Warning("Settings of type {SettingsType} validation failed", Helpers.FriendlyTypeName<TOptions>());
            }
        }
        if (failuresCount == 0)
            _logger.Debug("Settings of type {SettingsType} are valid", Helpers.FriendlyTypeName<TOptions>());
    }
}
