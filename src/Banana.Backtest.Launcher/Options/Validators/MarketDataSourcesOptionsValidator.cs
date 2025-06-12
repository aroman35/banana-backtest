using FluentValidation;

namespace Banana.Backtest.Launcher.Options.Validators;

public class MarketDataSourcesOptionsValidator : AbstractValidator<MarketDataSourcesOptions>
{
    public MarketDataSourcesOptionsValidator()
    {
        RuleFor(x => x.MarketDataDirectory).NotNull().WithMessage("Market data directory is required");
        RuleFor(x => x.MarketDataDirectory).Must(Directory.Exists).WithMessage("Market data directory not found");
        RuleFor(x => x.CacheDirectory).NotNull().WithMessage("Cache directory is required");
    }
}
