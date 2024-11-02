using System;
using Microsoft.Extensions.Logging;

namespace Swordfish.Library.Diagnostics;

public class LogListenerLogger(in LogListener logListener, in string categoryName) : ILogger
{
    private readonly LogListener _logListener = logListener;
    private readonly string _categoryName = categoryName;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        string log = formatter(state, exception);
        _logListener.Raise(new LogEventArgs(_categoryName, logLevel, log));
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }
}