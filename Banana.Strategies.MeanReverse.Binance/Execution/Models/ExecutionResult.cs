using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Emulator.ExchangeEmulator;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures.Socket;

namespace Banana.Strategies.MeanReverse.Binance.Execution.Models;

public class ExecutionResult
{
    private ExecutionResult()
    {
    }

    public static ExecutionResult NotPerformed(string reason)
    {
        return new ExecutionResult
        {
            Error = reason,
        };
    }

    public Guid Id { get; private init; }
    public string Symbol { get; private init; } = null!;
    public ConcurrentDictionary<Guid, UserOrder> PlacedOrders { get; private set; } = new();
    public ConcurrentDictionary<Guid, UserOrder> PendingOrders { get; private set; } = new();
    public ConcurrentDictionary<Guid, HashSet<UserTrade>> TradesByClientOrderId { get; private set; } = new();
    public HashSet<Guid> NotConfirmedOrderIds { get; private set; } = [];
    public ICollection<UserTrade> AllTrades => TradesByClientOrderId.Values.SelectMany(x => x).ToHashSet();
    public ICollection<UserOrder> AllOrders => PlacedOrders.Values.ToHashSet();
    public DateTime ExecutionStartedTime { get; private set; }
    public DateTime? ExecutionFinishedTime { get; private set; }
    public decimal RequestedQuantity { get; private init; }
    public decimal RemainedQuantity { get; private set; }
    public decimal ExecutedQuantity { get; private set; }
    public decimal MeanExecutionPrice { get; private set; }
    public Side Side { get; private init; }
    public ExecutionStatus Status { get; private set; }
    public string? Error { get; private set; }
    public TimeSpan Duration { get; private set; }
    public decimal Volume { get; private set; }

    [Flags]
    public enum ExecutionStatus
    {
        Unknown = 1 << 0,
        Created = 1 << 1,
        Running = 1 << 2,
        Finished = 1 << 3,
        Canceled = 1 << 4,
        Failed = 1 << 5,

        RunToCompletion = Finished | Canceled | Failed
    }

    public static ExecutionResult Created(LaunchExecutionCommandBase command, DateTime time, Guid id)
    {
        return new ExecutionResult
        {
            Id = id,
            Symbol = command.Symbol,
            Side = command.Side,
            RequestedQuantity = command.Quantity,
            RemainedQuantity = command.Quantity,
            Status = ExecutionStatus.Created,
            ExecutionStartedTime = time
        };
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool TradeReceived(UserTrade trade)
    {
        if (!NotConfirmedOrderIds.Contains(trade.ClientOrderId) && !PlacedOrders.ContainsKey(trade.ClientOrderId))
            return false;

        if (TradesByClientOrderId.GetOrAdd(trade.ClientOrderId, _ => []).Add(trade))
        {
            RemainedQuantity -= trade.Quantity;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void OrderPlaced(Guid clientOrderId)
    {
        NotConfirmedOrderIds.Add(clientOrderId);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void OrderSafeRejected(Guid clientOrderId)
    {
        NotConfirmedOrderIds.Remove(clientOrderId);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool OrderUpdateReceived(UserOrder order)
    {
        if (NotConfirmedOrderIds.Remove(order.ClientOrderId) || PlacedOrders.ContainsKey(order.ClientOrderId))
        {
            if (order.Status is Backtest.Emulator.Contracts.OrderStatus.New)
            {
                PendingOrders.TryAdd(order.ClientOrderId, order);
            }

            if (order.Status.IsCompleted())
            {
                PendingOrders.Remove(order.ClientOrderId, out _);
            }

            PlacedOrders[order.ClientOrderId] = order;
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Launched()
    {
        Status = ExecutionStatus.Running;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void FinishSuccess(DateTime time)
    {
        Status = ExecutionStatus.Finished;
        FinishAndCalculate(time);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void FinishFailure(DateTime time, string error)
    {
        Status = ExecutionStatus.Failed;
        Error = error;
        FinishAndCalculate(time);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void FinishCanceled(DateTime time)
    {
        Status = ExecutionStatus.Canceled;
        FinishAndCalculate(time);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Combine(ExecutionResult nestedExecutionResult)
    {
        if (nestedExecutionResult.Symbol != Symbol)
            throw new InvalidOperationException($"Symbol {nestedExecutionResult.Symbol} does not match Symbol {Symbol}");
        if (nestedExecutionResult.Side != Side)
            throw new InvalidOperationException($"Side {nestedExecutionResult.Side} does not match Side {Side}");
        if (!nestedExecutionResult.Status.IsCompleted())
            throw new InvalidOperationException("Nested execution result must be in completed status");
        if (nestedExecutionResult.ExecutionFinishedTime is null)
            throw new InvalidOperationException("Nested execution finished time is null");

        foreach (var (clientOrderId, order) in nestedExecutionResult.PlacedOrders)
        {
            PlacedOrders.TryAdd(clientOrderId, order);
        }

        foreach (var (clientOrderId, trades) in nestedExecutionResult.TradesByClientOrderId)
        {
            TradesByClientOrderId.TryAdd(clientOrderId, trades);
        }

        if (nestedExecutionResult.ExecutionStartedTime < ExecutionStartedTime)
            ExecutionStartedTime = nestedExecutionResult.ExecutionStartedTime;
        RemainedQuantity += nestedExecutionResult.RemainedQuantity;
        ExecutedQuantity += nestedExecutionResult.ExecutedQuantity;
        if (!string.IsNullOrWhiteSpace(nestedExecutionResult.Error))
            Error = nestedExecutionResult.Error;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool IsExecutionComplete()
    {
        if (NotConfirmedOrderIds.Any())
            return false;
        if (!IsOrdersClosed())
            return false;

        return RemainedQuantity <= 0;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool IsOrdersClosed()
    {
        if (!PendingOrders.IsEmpty)
            return false;
        foreach (var (clientOrderId, order) in PlacedOrders)
        {
            if (!order.Status.IsCompleted())
                return false;
            if (order.Status is not Backtest.Emulator.Contracts.OrderStatus.Fill)
                continue;
            if (!TradesByClientOrderId.TryGetValue(clientOrderId, out var trades))
                return false;
            if (order.Quantity != trades.Sum(x => x.Quantity))
                return false;
        }
        return true;
    }

    private void FinishAndCalculate(DateTime time)
    {
        ExecutionFinishedTime = time;
        ExecutedQuantity = AllTrades.Sum(t => t.Quantity);
        Volume = AllTrades.Sum(t => t.Quantity * t.Price);
        MeanExecutionPrice = ExecutedQuantity != 0 ? Volume / ExecutedQuantity : 0;
        Duration = time - ExecutionStartedTime;
    }
}

public static class ExecutionStatusExtensions
{
    public static bool IsCompleted(this ExecutionResult.ExecutionStatus value)
    {
        return (ExecutionResult.ExecutionStatus.RunToCompletion & value) == value;
    }

    public static bool IsCompleted(this Backtest.Emulator.Contracts.OrderStatus value)
    {
        return (Backtest.Emulator.Contracts.OrderStatus.FinalState & value) == value;
    }
}

public record struct UserTrade(
    long Id,
    long OrderId,
    DateTime TransactionTime,
    decimal Quantity,
    decimal Price,
    Guid ClientOrderId,
    Side Side)
{
    public static UserTrade FromBinance(BinanceFuturesStreamTradeUpdate userTrade)
    {
        return new UserTrade(
            userTrade.TradeId,
            userTrade.OrderId,
            userTrade.TransactionTime,
            Math.Abs(userTrade.QuantityOfLastFilledTrade),
            Math.Abs(userTrade.PriceLastFilledTrade),
            Guid.TryParse(userTrade.ClientOrderId, out var clientOrderId) ? clientOrderId : Guid.Empty,
            userTrade.Side is OrderSide.Buy ? Side.Long : Side.Short);
    }
}

public record struct UserOrder(
    long Id,
    Guid ClientOrderId,
    Side Side,
    decimal Price,
    decimal Quantity,
    OrderType Type,
    Banana.Backtest.Emulator.Contracts.OrderStatus Status,
    DateTime UpdateTime)
{
    public static UserOrder FromBinance(BinanceFuturesStreamOrderUpdateData userOrder)
    {

        return new UserOrder(
            userOrder.OrderId,
            Guid.TryParse(userOrder.ClientOrderId, out var clientOrderId) ? clientOrderId : Guid.Empty,
            userOrder.Side is OrderSide.Buy ? Side.Long : Side.Short,
            Math.Abs(userOrder.Price),
            Math.Abs(userOrder.Quantity),
            ConvertOrderType(userOrder.Type),
            ConvertOrderStatus(userOrder.Status),
            userOrder.UpdateTime);
    }

    private static Banana.Backtest.Emulator.Contracts.OrderStatus ConvertOrderStatus(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.New => Backtest.Emulator.Contracts.OrderStatus.New,
            OrderStatus.Rejected => Backtest.Emulator.Contracts.OrderStatus.Rejected,
            OrderStatus.Canceled => Backtest.Emulator.Contracts.OrderStatus.Cancelled,
            OrderStatus.Filled => Backtest.Emulator.Contracts.OrderStatus.Fill,
            OrderStatus.PartiallyFilled => Backtest.Emulator.Contracts.OrderStatus.PartiallyFill,
            _ => Backtest.Emulator.Contracts.OrderStatus.Unspecified
        };
    }

    private static OrderType ConvertOrderType(FuturesOrderType orderType)
    {
        return orderType switch
        {
            FuturesOrderType.Limit => OrderType.Limit,
            FuturesOrderType.Market => OrderType.Market,
            _ => OrderType.Market
        };
    }
}
