using System.Threading.Channels;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Services;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Launcher.Options;
using Banana.Backtest.Launcher.Steps.Common;
using Microsoft.Extensions.Options;

namespace Banana.Backtest.Launcher.Steps;

public class MarketDataStreamingStep : IBacktestStep
{
    private readonly ILogger _logger;
    private readonly IMarketDataCacheReader<LevelUpdate> _levelUpdatesCache;
    private readonly IMarketDataCacheReader<TradeUpdate> _tradesCache;
    private readonly ChannelWriter<MarketDataItem<LevelUpdate>> _levelUpdatesFeed;
    private readonly ChannelWriter<MarketDataItem<TradeUpdate>> _tradesFeed;
    private readonly IChannelsProvider _channelsProvider;
    private readonly IOptions<StrategyOptions> _strategyOptions;
    private readonly TaskCompletionSource _streamingStarted = new();
    private readonly MarketDataHash _hash;

    private int _tradesSent;
    private int _levelUpdatesSent;
    private long _currentTimestamp;

    public MarketDataStreamingStep(
        IChannelsProvider channelsProvider,
        ILogger logger,
        IOptions<MarketDataSourcesOptions> marketDataSourcesOptions,
        IOptions<StrategyOptions> strategyOptions)
    {
        _strategyOptions = strategyOptions;
        _logger = logger.ForContext<MarketDataStreamingStep>();
        _hash = MarketDataHash.Create(strategyOptions.Value.SymbolParsed, strategyOptions.Value.TradeDate);
        _channelsProvider = channelsProvider;
        _levelUpdatesCache = MarketDataCacheAccessorProvider.CreateReader<LevelUpdate>(
            marketDataSourcesOptions.Value.CacheDirectory,
            _hash.For(FeedType.LevelUpdates),
            marketDataSourcesOptions.Value.UseMmf);
        _tradesCache = MarketDataCacheAccessorProvider.CreateReader<TradeUpdate>(
            marketDataSourcesOptions.Value.CacheDirectory,
            _hash.For(FeedType.Trades),
            marketDataSourcesOptions.Value.UseMmf);
        _levelUpdatesFeed = channelsProvider.GetMarketDataSourceChannel<LevelUpdate>();
        _tradesFeed = channelsProvider.GetMarketDataSourceChannel<TradeUpdate>();
    }

    public int Priority => 3;
    public bool IsBackground => true;
    public Task IsInitialized => Task.CompletedTask;

    public async Task WaitForCompletion(CancellationToken cancellationToken = default)
    {
        _logger.Debug("Streaming is starting for {Hash}", _hash);
        if (_levelUpdatesCache.IsEmpty)
        {
            _logger.Warning("No level updates found");
            return;
        }

        if (_tradesCache.IsEmpty)
        {
            _logger.Warning("No trades found");
            return;
        }

        _streamingStarted.SetResult();
        foreach (var levelUpdate in _levelUpdatesCache.ContinueReadUntil())
        {
            _levelUpdatesSent++;
            try
            {
                Interlocked.CompareExchange(ref _currentTimestamp, levelUpdate.Timestamp, 0L);
                _logger.Verbose("Sending level update at timestamp {Timestamp}", levelUpdate.Timestamp.AsDateTime());
                await _channelsProvider.TimestampsFeed.Writer.WriteAsync(levelUpdate.Timestamp, cancellationToken);
                if (levelUpdate.Timestamp > _currentTimestamp)
                {
                    // Отправляем каждую ms для счетчика времени
                    while (++_currentTimestamp <= levelUpdate.Timestamp)
                    {
                        await _channelsProvider.TimestampsFeed.Writer.WaitToWriteAsync(cancellationToken);
                        await _channelsProvider.TimestampsFeed.Writer.WriteAsync(levelUpdate.Timestamp, cancellationToken);
                    }
                    await _channelsProvider.TimestampsFeed.Writer.WaitToWriteAsync(cancellationToken);
                    await _levelUpdatesFeed.WaitToWriteAsync(cancellationToken);
                    // Сделка !всегда! связана по времени с обновлением уровня и должна быть отправлена до него
                    foreach (var trade in _tradesCache.ContinueReadUntil(_currentTimestamp))
                    {
                        _logger.Verbose("Sending trade at timestamp {Timestamp}", levelUpdate.Timestamp);
                        await _tradesFeed.WriteAsync(trade, cancellationToken);
                        ++_tradesSent;
                    }
                }

                await _tradesFeed.WaitToWriteAsync(cancellationToken);
                await _levelUpdatesFeed.WriteAsync(levelUpdate, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error while processing source market data");
                throw;
            }
        }

        await Complete(cancellationToken);
        _logger.Information("Total trades sent: {TotalTrades} total level-updates sent: {TotalLevelUpdates}", _tradesSent, _levelUpdatesSent);
    }

    /// <summary>
    /// Ожидание завершения обработки всех подписок
    /// </summary>
    /// <param name="cancellationToken"></param>
    protected async Task Complete(CancellationToken cancellationToken)
    {
        _ = await _levelUpdatesFeed.WaitToWriteAsync(cancellationToken) && _levelUpdatesFeed.TryComplete();
        _logger.Debug("Level updates channel closed for {Hash}", _hash);
        _ = await _tradesFeed.WaitToWriteAsync(cancellationToken) && _tradesFeed.TryComplete();
        _logger.Debug("Trades channel closed for {Hash}", _hash);
        _ = await _channelsProvider.TimestampsFeed.Writer.WaitToWriteAsync(cancellationToken) && _channelsProvider.TimestampsFeed.Writer.TryComplete();
        _logger.Debug("Timestamp updates channel closed for {Hash}", _hash);
        _logger.Information("Completed streaming data");
    }
}
