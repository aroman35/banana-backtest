using Banana.Strategies.MeanReverse.Binance.DataFlow;
using Banana.Strategies.MeanReverse.Binance.Endpoints;
using Banana.Strategies.MeanReverse.Binance.Execution.Models;
using Banana.Strategies.MeanReverse.Binance.Extensions.Launchers.Cache;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Objects.Models.Futures;
using CryptoExchange.Net.Interfaces;
using Microsoft.Extensions.Options;

namespace Banana.Strategies.MeanReverse.Binance.Execution.CombinedExecution;

public class CombinedExecution<TLaunchExecutionCommand> : ExecutionBase<CombinedExecutionCommand<TLaunchExecutionCommand>>
    where TLaunchExecutionCommand : LaunchExecutionCommandBase
{
    private readonly DeferredExecution _deferredExecution;
    private readonly ILogger _logger;

    public CombinedExecution(
        DeferredExecution deferredExecution,
        DataFlowMediator mediator,
        IBinanceSocketClient binanceSocketClient,
        TimeProvider timeProvider,
        ICacheForSymbol<BinanceFuturesSymbol> instrumentsCache,
        ICacheForSymbol<ISymbolOrderBook> orderBooksCache,
        IOptionsSnapshot<ExecutionOptions> executionOptions,
        ILogger logger) : base(mediator, binanceSocketClient, instrumentsCache, orderBooksCache, timeProvider, executionOptions, logger)
    {
        _deferredExecution = deferredExecution;
        _logger = logger.ForContext<CombinedExecution<TLaunchExecutionCommand>>();
    }

    protected override async ValueTask Initialize(CancellationToken cancellationToken)
    {
        var quantityPerExecution = LaunchCommand.Quantity / 100.0M * LaunchCommand.PartSizePercent;
        var commands = new List<TLaunchExecutionCommand>();
        var remainedQuantity = LaunchCommand.Quantity;
        while (remainedQuantity > 0)
        {
            var quantity = AdjustQuantity(Math.Min(quantityPerExecution, remainedQuantity));
            var command = (TLaunchExecutionCommand)LaunchCommand.NestedExecutionsRules.Clone();
            command.Quantity = quantity;
            commands.Add(command);
            remainedQuantity -= quantity;
        }
        _logger.Information("Totally prepared {Count} nested executions", commands.Count);
        switch (LaunchCommand.Type)
        {
            case CombinedExecutionType.Synced:
                foreach (var command in commands)
                {
                    var result = await _deferredExecution.Execute(command, cancellationToken);
                    Result.Combine(result);
                }
                Finish();
                break;
            case CombinedExecutionType.Parallel:
                var results = await Task.WhenAll(commands.Select(x => _deferredExecution.Execute(x, cancellationToken)));
                foreach (var result in results)
                {
                    Result.Combine(result);
                }
                Finish();
                break;
            case CombinedExecutionType.Chunked:
                ArgumentNullException.ThrowIfNull(LaunchCommand.ChunkSize);
                foreach (var commandsChunk in commands.Chunk(LaunchCommand.ChunkSize.Value))
                {
                    var chunkResults = await Task.WhenAll(commandsChunk.Select(x => _deferredExecution.Execute(x, cancellationToken)));
                    foreach (var result in chunkResults)
                    {
                        Result.Combine(result);
                    }
                }
                Finish();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
