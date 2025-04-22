using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Emulator.Abstractions;
using Serilog;

namespace Banana.Backtest.Emulator.Services;

/// <summary>
/// Реализация часов на основе тиков исторических рыночных данных
/// </summary>
public class BacktestClock : TimeProvider, IChannelSubscriber<long>
{
    private readonly Task _timestampsFeedTask;
    private long _currentTimestampMilliseconds;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="channelsProvider">Канал для поставки обновления тиков</param>
    /// <param name="logger">Логгер</param>
    public BacktestClock(IChannelsProvider channelsProvider, ILogger logger)
    {
        Logger = logger.ForContext<BacktestClock>();
        Reader = channelsProvider.TimestampsFeed.Reader;
        _timestampsFeedTask = ((IChannelSubscriber<long>)this).SubscribeAsync();
    }

    /// <inheritdoc />
    public ILogger Logger { get; }

    /// <inheritdoc />
    public ChannelReader<long> Reader { get; }

    /// <inheritdoc />
    public override long GetTimestamp() => _currentTimestampMilliseconds;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _currentTimestampMilliseconds.AsDateTime();

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.Synchronized)]
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        return new BacktestTimer(callback, state, dueTime, period, Reader, Logger);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.Synchronized)]
    public ValueTask HandleChannelDataAsync(long channelData, CancellationToken cancellationToken)
    {
        _currentTimestampMilliseconds = channelData;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _timestampsFeedTask;
        Logger.Debug("Disposed");
    }

    /// <summary>
    /// Таймер основанный на тиках исторических рыночных данных
    /// </summary>
    private class BacktestTimer : ITimer, IChannelSubscriber<long>
    {
        private readonly Task _timestampsFeedTask;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly TimerCallback _callback;
        private readonly object? _state;

        private TimeSpan _dueTime;
        private TimeSpan _period;
        private long _startedTimestampMilliseconds;
        private long _lastFired;

        /// <inheritdoc />
        public ILogger Logger { get; }

        /// <inheritdoc />
        public ChannelReader<long> Reader { get; }

        /// <summary>
        /// ИД-таймера
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="callback">Вызываемый метод при активации таймера</param>
        /// <param name="state">Замыкание на аргумент для метода</param>
        /// <param name="dueTime">Сдвиг запуска</param>
        /// <param name="period">Круг таймера</param>
        /// <param name="timestampsFeed">Канал получения данных о смене тиков</param>
        /// <param name="logger">Логгер</param>
        public BacktestTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period,
            ChannelReader<long> timestampsFeed,
            ILogger logger)
        {
            Logger = logger.ForContext<BacktestTimer>();
            Id = Guid.NewGuid();
            _callback = callback;
            _state = state;
            _dueTime = dueTime;
            _period = period;
            Reader = timestampsFeed;
            _timestampsFeedTask = ((IChannelSubscriber<long>)this).SubscribeAsync(_cancellationTokenSource.Token);
            Logger.Debug("Timer {Id} created and subscribed started", Id);
        }

        /// <inheritdoc />
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _dueTime = dueTime;
            _period = period;

            return true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            Logger.Debug("Timer {Id} disposed", Id);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            Dispose();
            await _timestampsFeedTask;
        }

        /// <inheritdoc />
        public ValueTask HandleChannelDataAsync(long channelData, CancellationToken cancellationToken)
        {
            if (_startedTimestampMilliseconds == 0)
                _startedTimestampMilliseconds = channelData;

            if (channelData - _startedTimestampMilliseconds < _dueTime.TotalMilliseconds)
            {
                return ValueTask.CompletedTask;
            }

            var lastFired = _lastFired == 0 ? _startedTimestampMilliseconds : _lastFired;

            if (channelData - lastFired >= _period.TotalMilliseconds)
            {
                Logger.Debug("Timer {Id} is firing [{Time}]", Id, channelData.AsDateTime());
                _callback.Invoke(_state);
                _lastFired = channelData;
            }
            return ValueTask.CompletedTask;
        }
    }
}
