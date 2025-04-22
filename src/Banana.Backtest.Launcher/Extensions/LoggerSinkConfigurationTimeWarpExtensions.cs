using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace Banana.Backtest.Launcher.Extensions;

public static class LoggerSinkConfigurationTimeWarpExtensions
{
    public static LoggerConfiguration TimeWarp(this LoggerSinkConfiguration loggerSinkConfiguration, Func<LogEvent, DateTimeOffset> getTimestamp, Action<LoggerSinkConfiguration> configure)
    {
        return LoggerSinkConfiguration.Wrap(loggerSinkConfiguration, sink => new TimeWarpSink(sink, getTimestamp), configure);
    }
}
