using System.Threading.Channels;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.Common.Services;
using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;
using Microsoft.Extensions.Options;
using Serilog;

namespace Banana.Backtest.Emulator.Services;

/// <summary>
/// Сервис чтения и дальнейшей маршрутизации рыночных данных
/// </summary>
public class MarketDataProvider
{
    private readonly ILogger _logger;
    private readonly IMarketDataCacheReader<LevelUpdate> _levelUpdatesCache;
    private readonly IMarketDataCacheReader<TradeUpdate> _tradesCache;
    private readonly ChannelWriter<MarketDataItem<LevelUpdate>> _levelUpdatesFeed;
    private readonly ChannelWriter<MarketDataItem<TradeUpdate>> _tradesFeed;

    private long _currentTimestamp;
    private readonly IChannelsProvider _channelsProvider;
    private readonly MarketDataHash _hash;

    private int _tradesSent;
    private int _levelUpdatesSent;

    /// <summary>
    /// Сервис чтения и дальнейшей маршрутизации рыночных данных
    /// </summary>
    /// <param name="channelsProvider">Поставщик каналов</param>
    /// <param name="settings">Настройки</param>
    /// <param name="logger">Логгер</param>
    public MarketDataProvider(
        IChannelsProvider channelsProvider,
        IOptions<MarketDataSettings> settings,
        ILogger logger)
    {
        _logger = logger.ForContext<MarketDataProvider>();
        var symbol = Symbol.Create(Asset.Get(settings.Value.Ticker), Asset.Get(settings.Value.ClassCode), settings.Value.Exchange);
        _hash = MarketDataHash.Create(symbol, settings.Value.TradeDate);
        _channelsProvider = channelsProvider;
        _levelUpdatesCache = MarketDataCacheAccessorProvider.CreateReader<LevelUpdate>(settings.Value.MarketDataDirectory, _hash.For(FeedType.LevelUpdates), true);
        _tradesCache = MarketDataCacheAccessorProvider.CreateReader<TradeUpdate>(settings.Value.MarketDataDirectory, _hash.For(FeedType.Trades), true);
        _levelUpdatesFeed = channelsProvider.GetMarketDataSourceChannel<LevelUpdate>();
        _tradesFeed = channelsProvider.GetMarketDataSourceChannel<TradeUpdate>();
    }

    /// <summary>
    /// Метод чтения и процессинга рыночных данных
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task ExecuteStreamingAsync(CancellationToken cancellationToken)
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

        foreach (var levelUpdate in _levelUpdatesCache.ContinueReadUntil())
        {
            _levelUpdatesSent++;
            try
            {
                Interlocked.CompareExchange(ref _currentTimestamp, levelUpdate.Timestamp, 0L);
                _logger.Verbose("Sending level update at timestamp {Timestamp}", levelUpdate.Timestamp);
                await _channelsProvider.TimestampsFeed.Writer.WriteAsync(levelUpdate.Timestamp, cancellationToken);
                if (levelUpdate.Timestamp > _currentTimestamp)
                {
                    // _currentTimestamp = levelUpdate.Timestamp;
                    // await _channelsProvider.TimestampsFeed.Writer.WriteAsync(levelUpdate.Timestamp, cancellationToken);
                    // Отправляем каждую ms для счетчика времени
                    while (++_currentTimestamp <= levelUpdate.Timestamp)
                    {
                        _logger.Verbose("Sending timestamp update: {Timestamp}", levelUpdate.Timestamp);
                        await _channelsProvider.TimestampsFeed.Writer.WaitToWriteAsync(cancellationToken);
                        await _channelsProvider.TimestampsFeed.Writer.WriteAsync(levelUpdate.Timestamp, cancellationToken);
                    }
                    await _channelsProvider.TimestampsFeed.Writer.WaitToWriteAsync(cancellationToken);
                    await _levelUpdatesFeed.WaitToWriteAsync(cancellationToken);
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
