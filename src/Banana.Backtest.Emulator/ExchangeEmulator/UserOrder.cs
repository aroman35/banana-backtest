using System.Runtime.InteropServices;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;

namespace Banana.Backtest.Emulator.ExchangeEmulator;

[StructLayout(LayoutKind.Sequential)]
public struct UserOrder
{
    public long Id;
    public OrderType OrderType;
    public Side Side;
    public double Price;
    public double Quantity;
    public long Timestamp;
    public Guid ClientOrderId;

    public UserOrder PartiallyFill(double executedQuantity)
    {
        return this with { Quantity = Quantity - executedQuantity };
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct UserExecution
{
    public long TradeId;
    public long OrderId;
    public Side Side;
    public double ExecutionPrice;
    public double ExecutedQuantity;
    public long Timestamp;

    public static UserExecution OrderFullFill(UserOrder order)
    {
        return new UserExecution
        {
            TradeId = Helpers.NextId,
            OrderId = order.Id,
            Side = order.Side,
            ExecutionPrice = order.Price,
            ExecutedQuantity = order.Quantity,
            Timestamp = Helpers.Timestamp
        };
    }
    
    public static unsafe UserExecution OrderPartiallyFill(UserOrder* orderPtr, double executedQuantity)
    {
        var execution = new UserExecution
        {
            TradeId = Helpers.NextId,
            OrderId = orderPtr->Id,
            Side = orderPtr->Side,
            ExecutionPrice = orderPtr->Price,
            ExecutedQuantity = executedQuantity,
            Timestamp = Helpers.Timestamp
        };
        *orderPtr = orderPtr->PartiallyFill(executedQuantity);
        return execution;
    }
}

public enum OrderType
{
    Market,
    Limit
}