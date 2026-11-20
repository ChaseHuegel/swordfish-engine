using System.Text;

namespace Shoal.Logging;

/// <summary>
///     Category logger backed by <see cref="AsyncFileLoggerProvider"/>.
/// </summary>
internal sealed class AsyncFileLogger(in AsyncFileLoggerProvider provider, in string categoryName, in LogLevel minLevel) : ILogger
{
    private readonly AsyncFileLoggerProvider _provider = provider;
    private readonly string _categoryName = categoryName;
    private readonly LogLevel _minLevel = minLevel;

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None && logLevel >= _minLevel;
    }

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception == null)
        {
            return;
        }

        var builder = new StringBuilder(160);
        builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        builder.Append(" [");
        builder.Append(FormatLevel(logLevel));
        builder.Append("] [");
        string? threadName = Thread.CurrentThread.Name;
        if (!string.IsNullOrEmpty(threadName))
        {
            builder.Append(threadName);
        }
        else
        {
            builder.Append("T");
            builder.Append(Environment.CurrentManagedThreadId);
        }

        builder.Append("] ");
        builder.Append(_categoryName);
        builder.Append(": ");
        builder.AppendLine(message);

        if (exception != null)
        {
            builder.AppendLine(exception.ToString());
        }

        _provider.Enqueue(logLevel, builder.ToString());
    }

    private static string FormatLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "LOG",
        };
    }
}
