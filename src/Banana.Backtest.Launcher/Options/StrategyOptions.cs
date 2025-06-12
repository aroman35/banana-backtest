using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.Launcher.Options;

public class StrategyOptions
{
    private Symbol? _symbol;
    public string Symbol { get; set; } = null!;
    public DateOnly TradeDate { get; set; }
    public Symbol SymbolParsed => _symbol ??= Banana.Backtest.Common.Models.Root.Symbol.Parse(Symbol);
}
