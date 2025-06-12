using Banana.Backtest.Common.Models.Root;
using FluentValidation;

namespace Banana.Backtest.Launcher.Options.Validators;

public class StrategyOptionsValidator : AbstractValidator<StrategyOptions>
{
    public StrategyOptionsValidator()
    {
        RuleFor(x => x.Symbol)
            .NotNull()
            .Must(IsSymbolValid).WithMessage((_, symbolString) => $"{symbolString} is not a valid symbol");
    }

    private static bool IsSymbolValid(string symbol)
    {
        try
        {
            Symbol.Parse(symbol);
        }
        catch
        {
            return false;
        }
        return true;
    }
}
