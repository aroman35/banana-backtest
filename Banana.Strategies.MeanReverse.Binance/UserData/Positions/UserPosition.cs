using Banana.Strategies.MeanReverse.Binance.DataFlow;
using Banana.Strategies.MeanReverse.Binance.Execution;
using Banana.Strategies.MeanReverse.Binance.Execution.Fast;
using Banana.Strategies.MeanReverse.Binance.Execution.Models;
using Banana.Strategies.MeanReverse.Binance.Extensions;
using Binance.Net.Objects.Models.Futures;
using Binance.Net.Objects.Models.Futures.Socket;
using static Banana.Strategies.MeanReverse.Binance.Extensions.JobsExtensions;

namespace Banana.Strategies.MeanReverse.Binance.UserData.Positions;

public class UserPosition : IAsyncDisposable
{
    private readonly DeferredExecution _deferredExecution;
    private readonly DataFlowMediator _mediator;
    private readonly ILogger _logger;
    private readonly Task _streamUpdatesTask;

    public UserPosition(
        BinancePositionDetailsUsdt positionDetails,
        DeferredExecution deferredExecution,
        DataFlowMediator mediator,
        CancellationToken cancellationToken,
        ILogger logger)
    {
        _logger = logger.ForContext<UserPosition>().ForContext("Symbol", positionDetails.Symbol);
        State = new PositionState(positionDetails);
        _deferredExecution = deferredExecution;
        _mediator = mediator;
        _streamUpdatesTask = HandleStreamData(
            mediator.StreamForSymbol<BinanceFuturesStreamPosition>(positionDetails.Symbol),
            (state, _) =>
            {
                State.Update(state);
                _logger.Debug("[{Symbol}] position updated {Side} {Quantity}", State.Symbol, State.Side, State.Quantity);
                return ValueTask.CompletedTask;
            },
            cancellationToken: cancellationToken);
    }

    public PositionState State { get; }

    public async ValueTask<ExecutionResult> CloseAsync(CancellationToken cancellationToken)
    {
        if (State.Quantity == 0)
        {
            _logger.Warning("[{Symbol}] requested to close null position", State.Symbol);
            return ExecutionResult.NotPerformed("No quantity available for position");
        }
        var result = await _deferredExecution.Execute(new FastExecutionLaunchCommand
        {
            Quantity = State.Quantity,
            Symbol = State.Symbol,
            QuantitySpread = QuantitySpread.NoSpread,
            Side = State.Side.Revert(),
            Type = FastExecutionType.Market,
            Timeout = TimeSpan.FromSeconds(600)
        }, cancellationToken);
        if (result.Status is ExecutionResult.ExecutionStatus.Finished)
        {
            _logger.Information("Position closed");
            return result;
        }
        _logger.Error("Position was not closed: {Error}", result.Error);
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        await _streamUpdatesTask;
    }
}
