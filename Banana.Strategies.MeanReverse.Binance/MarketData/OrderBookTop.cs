namespace Banana.Strategies.MeanReverse.Binance.MarketData;

public readonly record struct OrderBookTop(OrderBookEntry BestBid, OrderBookEntry BestAsk);
