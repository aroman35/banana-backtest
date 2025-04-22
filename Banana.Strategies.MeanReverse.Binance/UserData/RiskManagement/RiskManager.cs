using Banana.Backtest.Common.Models;
using Banana.Strategies.MeanReverse.Binance.DataFlow;
using Banana.Strategies.MeanReverse.Binance.Execution;
using Banana.Strategies.MeanReverse.Binance.Execution.FrontRun;
using Banana.Strategies.MeanReverse.Binance.Execution.Models;
using Banana.Strategies.MeanReverse.Binance.Extensions;
using Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;
using Banana.Strategies.MeanReverse.Binance.MarketData;
using Banana.Strategies.MeanReverse.Binance.UserData.Positions;
using Binance.Net.Objects.Models.Futures;
using static Banana.Strategies.MeanReverse.Binance.Extensions.JobsExtensions;

namespace Banana.Strategies.MeanReverse.Binance.UserData.RiskManagement;

public class RiskManager : IAsyncDisposable
{
    private readonly DataFlowMediator _mediator;
    private readonly ICacheForSymbol<UserPosition> _positionsCache;
    private readonly ICacheForSymbol<BinanceFuturesSymbol> _instrumentsCache;
    private readonly DeferredExecution _deferredExecution;
    private readonly TaskCompletionSource _completion = new();

    private RiskManagerState _state = null!;

    private CancellationTokenSource _cancellationTokenSource = null!;
    private ILogger _logger;
    private Task _positionsStreamReaderTask = null!;
    private Task _orderBookTopReaderTask = null!;

    public RiskManager(
        DataFlowMediator mediator,
        ICacheForSymbol<UserPosition> positionsCache,
        ICacheForSymbol<BinanceFuturesSymbol> instrumentsCache,
        DeferredExecution deferredExecution,
        ILogger logger)
    {
        _mediator = mediator;
        _positionsCache = positionsCache;
        _instrumentsCache = instrumentsCache;
        _deferredExecution = deferredExecution;
        _logger = logger.ForContext<RiskManager>();
    }

    public Task Completion => _completion.Task;

    public void Configure(CreateRiskManagementCommand command, CancellationToken cancellationToken)
    {
        var position = _positionsCache.Get(command.Symbol);
        var instrument = _instrumentsCache.Get(command.Symbol);

        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(position);

        var positionsStreamReader = _mediator.StreamForSymbol<PositionState>(command.Symbol, cancellationToken);
        var orderBookTopReader = _mediator.StreamForSymbol<OrderBookTop>(command.Symbol, cancellationToken);

        _state = new RiskManagerState(instrument, position, command);
        _logger = _logger
            .ForContext("Symbol", command.Symbol)
            .ForContext("RiskManagementId", _state.Id)
            .ForContext("ExecutionId", command.ConnectedExecutionId);
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationTokenSource.Token.Register(() => _completion.TrySetResult());

        _positionsStreamReaderTask = HandleStreamData(
            positionsStreamReader,
            OnPositionUpdated,
            OnError,
            _cancellationTokenSource.Token);

        _orderBookTopReaderTask = HandleStreamData(
            orderBookTopReader,
            OnOrderBookUpdated,
            OnError,
            _cancellationTokenSource.Token);
    }

    private async ValueTask OnPositionUpdated(PositionState positionState, CancellationToken cancellationToken)
    {
        if (_state.Position.State.Quantity == 0)
        {
            _logger.Information("Closing risk management as the position was closed");
            await _cancellationTokenSource.CancelAsync();
        }
    }

    private async ValueTask OnOrderBookUpdated(OrderBookTop orderBookTop, CancellationToken cancellationToken)
    {
        if (_state.Position.State.Quantity == 0)
        {
            _logger.Information("Position is already closed");
            await _cancellationTokenSource.CancelAsync();
            return;

        }
        var currentPrice = (_state.Side is Side.Long ? orderBookTop.BestAsk : orderBookTop.BestBid).Price;
        if (currentPrice == _state.CurrentPrice)
            return;

        var extremumPrice = _state.ExtremumPrice;
        var stopLossPrice = _state.StopLossPrice;
        _state.PriceUpdated(currentPrice);
        if (extremumPrice != _state.ExtremumPrice)
            _logger.Verbose("Extremum price changed: {Last} -> {New}", extremumPrice, _state.ExtremumPrice);
        if (stopLossPrice != _state.StopLossPrice)
            _logger.Verbose("Stop loss price changed: {Last} -> {New}", stopLossPrice, _state.StopLossPrice);

        if (_state.IsStopLossTriggered)
        {
            _logger.Information("Closing position by stop loss");
            var result = await _deferredExecution.Execute(new FrontRunExecutionLaunchCommand
            {
                Symbol = _state.Symbol,
                IsMakerOnly = true,
                Quantity = _state.Position.State.Quantity,
                QuantitySpread = QuantitySpread.NoSpread,
                Side = _state.Side.Revert(),
                Timeout = TimeSpan.FromSeconds(600),
                IcebergDivider = 1
            }, _cancellationTokenSource.Token);

            if (result.Error is null)
            {
                _logger.Information("Position closed with price {Price}x{Quantity}", result.MeanExecutionPrice, result.ExecutedQuantity);
                return;
            }

            await _state.Position.CloseAsync(cancellationToken);
        }
    }

    private async Task OnError(Exception exception)
    {
        if (exception is TaskCanceledException)
        {
            _logger.Warning("Risk management cancelled");
            _completion.TrySetResult();
            return;
        }
        var result = await _state.Position.CloseAsync(CancellationToken.None);
        if (result.Error is null)
        {
            _logger.Warning(exception, "Position closed with price {Price}x{Quantity}", result.MeanExecutionPrice, result.ExecutedQuantity);
        }
        _logger.Error(exception, "Risk management failed and position couldn't be closed: {Reason}", result.Error);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _positionsStreamReaderTask;
            await _orderBookTopReaderTask;
        }
        catch
        {
            // ignored
        }

        if (_state.Position.State.Quantity > 0)
            await _state.Position.CloseAsync(CancellationToken.None);
        _logger.Information("Risk management closed");
    }
}
