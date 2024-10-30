﻿using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Swordfish.Library.Diagnostics;

public class LogListener : ILoggerProvider, ILogListener
{
    public event EventHandler<LoggerEventArgs> NewLog;

#if DEBUG
    private readonly List<LoggerEventArgs> _history = [];
#endif
    
    public ILogger CreateLogger(string categoryName)
    {
        return new LogListenerLogger(this, categoryName);
    }

    public LoggerEventArgs[] GetHistory()
    {
#if DEBUG
        lock (_history)
        {
            return _history.ToArray();
        }
#else
        return Array.Empty<LoggerEventArgs>();
#endif
    }

    public void Dispose()
    {
        //  Do nothing.
    }
    
    internal void Raise(LoggerEventArgs e)
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