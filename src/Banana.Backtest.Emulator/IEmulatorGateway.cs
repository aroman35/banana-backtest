using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.ExchangeEmulator;

namespace Banana.Backtest.Emulator;

public interface IEmulatorGateway
{
    void OrderBookUpdated(MarketDataItem<OrderBookSnapshot> orderBookSnapshot);
    void AnonymousTradeReceived(MarketDataItem<TradeUpdate> trade);
    void UserExecutionReceived(UserExecution userExecution);
}