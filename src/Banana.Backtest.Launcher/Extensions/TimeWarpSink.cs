using Serilog.Core;
using Serilog.Events;

namespace Banana.Backtest.Launcher.Extensions;

public class TimeWarpSink(ILogEventSink target, Func<LogEvent, DateTimeOffset> getTimestamp)
    : IDisposable, ILogEventSink
{
    private readonly ILogEventSink _target = target ?? throw new ArgumentNullException(nameof(target));
    private readonly Func<LogEvent, DateTimeOffset> _getTimestamp = getTimestamp ?? throw new ArgumentNullException(nameof(getTimestamp));

    public void Dispose()
    {
        (_target as IDisposable)?.Dispose();
    }

    public void Emit(LogEvent logEvent)
    {
        var timestamp = _getTimestamp(logEvent);
        var surrogate = new LogEvent(timestamp, logEvent.Level, logEvent.Exception, logEvent.MessageTemplate,
            logEvent.Properties.Select(kv => new LogEventProperty(kv.Key, kv.Value)));
        _target.Emit(surrogate);
    }
}
