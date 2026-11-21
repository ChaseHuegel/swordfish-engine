using System.Threading;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Library.Diagnostics;

namespace WaywardBeyond.Client.Diagnostics;

/// <summary>Counts log entries for diagnostics.</summary>
internal sealed class LogCounters : IAutoActivate
{
    private long _warnings;
    private long _errors;
    private long _criticals;
    private long _others;

    /// <summary>Initializes counters that observe the supplied log listener.</summary>
    public LogCounters(LogListener logListener)
    {
        logListener.NewLog += OnNewLog;
    }

    /// <summary>Returns the current running totals.</summary>
    public LogCountersSnapshot GetSnapshot()
    {
        return new LogCountersSnapshot(
            Interlocked.Read(ref _warnings),
            Interlocked.Read(ref _errors),
            Interlocked.Read(ref _criticals),
            Interlocked.Read(ref _others)
        );
    }

    private void OnNewLog(object? sender, LogEventArgs e)
    {
        switch (e.LogLevel)
        {
            case LogLevel.Warning:
                Interlocked.Increment(ref _warnings);
                break;
            case LogLevel.Error:
                Interlocked.Increment(ref _errors);
                break;
            case LogLevel.Critical:
                Interlocked.Increment(ref _criticals);
                break;
            default:
                Interlocked.Increment(ref _others);
                break;
        }
    }
}