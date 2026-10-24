using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace WaywardBeyond.Networking.Rpc;

/// <summary>
/// Bridges the nsd-rpc drop and error events to the game logger. The sink is not gated by the
/// trace switch, so every library drop reaches the engine log.
/// </summary>
public static class RpcLogging
{
    /// <summary>Builds an <c>RpcOptions.LogSink</c> delegate for one category.</summary>
    public static Action<TraceEventType, string, Exception?> ToLogSink(ILogger logger)
    {
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        return LogSinkCallback;
        void LogSinkCallback(TraceEventType level, string message, Exception? exception)
        {
            logger.Log(Map(level), exception, "{message}", message);
        }
    }

    private static LogLevel Map(TraceEventType level)
    {
        return level switch
        {
            TraceEventType.Critical => LogLevel.Critical,
            TraceEventType.Error => LogLevel.Error,
            TraceEventType.Warning => LogLevel.Warning,
            TraceEventType.Information => LogLevel.Information,
            TraceEventType.Verbose => LogLevel.Trace,
            _ => LogLevel.Debug,
        };
    }
}
