using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.Launcher.Exceptions;

public class MarketDataNotFoundException(Symbol symbol, DateOnly tradeDate, FeedType feedType)
    : Exception($"Market data of type {feedType} not found for symbol {symbol} at {tradeDate}");
