using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Swordfish.Library.Diagnostics;

public class LogListener : ILoggerProvider
{
    public event EventHandler<LogEventArgs> NewLog;

#if DEBUG
    private readonly List<LogEventArgs> _history = [];
#endif
    
    public ILogger CreateLogger(string categoryName)
    {
        return new LogListenerLogger(this, categoryName);
    }

    public LogEventArgs[] GetHistory()
    {
#if DEBUG
        lock (_history)
        {
            return _history.ToArray();
        }
#else
        return Array.Empty<LogEventArgs>();
#endif
    }

    public void Dispose()
    {
        //  Do nothing.
    }
    
    internal void Raise(LogEventArgs e)
    {
#if DEBUG
        lock (_history)
        {
            _history.Add(e);
        }
#endif
        
        NewLog?.Invoke(this, e);
    }
}