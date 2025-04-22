using Banana.Backtest.Common.Models;
using Banana.Backtest.Emulator.Contracts;

namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy.Runtime;

public interface IRiskManagementModule
{
    RiskStatus Status { get; }
    double PlannedOrderPrice { get; }
    double PlannedOrderQuantity { get; }
    double BalancePrice { get; }
    double BalanceQuantity { get; }
    double StopPrice { get; }
    double CurrentUnrealizedPnl { get; }
    double FeeExecuted { get; }
    Side Side { get; }

    bool CreateOrderRequest(double currentPrice);
    void ExecutionReceived(UserExecution execution);
}
