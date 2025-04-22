using Banana.Backtest.Common.Models;
using Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy.Runtime;
using Banana.Strategies.MeanReverse.Binance.DataFlow;
using Banana.Strategies.MeanReverse.Binance.Execution;
using Banana.Strategies.MeanReverse.Binance.Execution.FrontRun;
using Banana.Strategies.MeanReverse.Binance.Execution.Models;
using Banana.Strategies.MeanReverse.Binance.UserData.RiskManagement;
using Binance.Net.Objects.Models.Futures;
using Binance.Net.Objects.Models.Spot.Socket;
using Microsoft.Extensions.Options;

namespace Banana.Strategies.MeanReverse.Binance;

public class TestingStrategy
{
    private readonly DeferredExecution _deferredExecution;
    private readonly DeferredRiskManagement _deferredRiskManagement;
    private readonly DataFlowMediator _mediator;
    private readonly RuntimeReversal _runtimeReversal;
    private readonly ILogger _logger;
    private readonly IOptions<StrategySettings> _strategySettings;
    private Side _side;
    private Task? _currentExecution;
    private CancellationTokenSource? _executionCancellationTokenSource;

    public TestingStrategy(
        DeferredExecution deferredExecution,
        DeferredRiskManagement deferredRiskManagement,
        DataFlowMediator mediator,
        IOptions<StrategySettings> strategySettings,
        ILogger logger)
    {
        _deferredExecution = deferredExecution;
        _deferredRiskManagement = deferredRiskManagement;
        _mediator = mediator;
        _strategySettings = strategySettings;
        _logger = logger;
        _runtimeReversal = new RuntimeReversal(strategySettings.Value.ReversalThresholdPercent);
    }

    public async Task ExecuteAsync(BinanceFuturesSymbol instrument, CancellationToken stoppingToken)
    {
        var symbol = instrument.Name;
        _logger.Information("Starting strategy for {Symbol}", symbol);
        await foreach (var trade in _mediator.StreamForSymbol<BinanceStreamTrade>(symbol, stoppingToken))
        {
            var sideUpdate = (Side)(_runtimeReversal.FindTrendReversal(decimal.ToDouble(trade.Price)) * (_strategySettings.Value.ReverseTrend ? -1 : 1));
            if (_side != sideUpdate)
            {
                if (_side is not Side.Undefined)
                {
                    _logger.Information("[{Symbol}]: side changed: {Previous} => {Updated}", trade.Symbol, _side, sideUpdate);
                    if (_currentExecution is not null && _executionCancellationTokenSource is not null && !_currentExecution.IsCompleted)
                    {
                        await _executionCancellationTokenSource.CancelAsync();
                        _executionCancellationTokenSource.Dispose();
                    }
                    _executionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    var minQuantityUnfiltered = instrument.MinNotionalFilter!.MinNotional / trade.Price * _strategySettings.Value.MinOrderMultiplier;
                    var quantity = minQuantityUnfiltered - minQuantityUnfiltered % instrument.LotSizeFilter!.StepSize;
                    _currentExecution = Task.Run(async () =>
                    {
                        try
                        {
                            var result = await _deferredExecution.Execute(new FrontRunExecutionLaunchCommand
                            {
                                Symbol = trade.Symbol,
                                Side = sideUpdate,
                                IsMakerOnly = _strategySettings.Value.IsMakerOnly,
                                Quantity = quantity,
                                Timeout = _strategySettings.Value.OrderTimeout,
                                QuantitySpread = QuantitySpread.NoSpread,
                                IcebergDivider = _strategySettings.Value.IcebergDivider
                            }, _executionCancellationTokenSource.Token);
                            if (result.ExecutedQuantity >= 0)
                            {
                                await _deferredRiskManagement.Execute(new CreateRiskManagementCommand
                                {
                                    Symbol = trade.Symbol,
                                    InitialPrice = result.MeanExecutionPrice,
                                    InitialQuantity = result.ExecutedQuantity,
                                    StopLossPercent = _strategySettings.Value.StopLossPercent,
                                    ConnectedExecutionId = result.Id
                                }, _executionCancellationTokenSource.Token);
                            }
                        }
                        catch (TaskCanceledException)
                        {
                        }
                        catch (Exception exception)
                        {
                            _logger.Error(exception, "[{Symbol}]: failed to execute strategy signal", trade.Symbol);
                        }
                    }, stoppingToken);
                }
                _side = sideUpdate;
            }
        }
    }
}
