using Banana.Backtest.Emulator.Contracts;

namespace Banana.Backtest.Emulator;

public class PreTradeControlException : Exception
{
    public static PreTradeControlException ForOrder(OrderInfo order)
    {
        return new PreTradeControlException();
    }
}
