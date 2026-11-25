using Microsoft.Extensions.Logging;
using Shoal.Logging;
using WaywardBeyond.AssetTools.Settings;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Manages asynchronous file logging for Asset Studio under the tool data logs directory.
/// </summary>
public sealed class StudioLogSession : IDisposable
{
    private const int MAX_RECENT_ERRORS = 40;

    private readonly AsyncFileLoggerProvider _provider;
    private readonly ILogger _logger;
    private readonly Lock _errorsLock = new();
    private readonly List<string> _recentErrors = [];
    private bool _disposed;

    /// <summary>
    ///     Starts a studio file logging session writing under <paramref name="logsDirectory"/>.
    /// </summary>
    public StudioLogSession(string logsDirectory, ToolLogLevel minLevel)
    {
        LogsDirectory = Path.GetFullPath(logsDirectory);
        var config = LoggingConfig.CreateDefault();
        config.MinLevel = MapLogLevel(minLevel);
        _provider = new AsyncFileLoggerProvider(config, LogsDirectory);
        _logger = _provider.CreateLogger("WaywardBeyond.AssetStudio");
        LogInfo($"Asset Studio logging started (level={minLevel}, file={_provider.TimestampedLogPath}).");
    }

    /// <summary>
    ///     Gets the full path to the active logs directory.
    /// </summary>
    public string LogsDirectory { get; }

    /// <summary>
    ///     Gets the full path to the active latest log file.
    /// </summary>
    public string LogFilePath => _provider.LatestLogPath;

    /// <summary>
    ///     Gets the full path to the timestamped session log file.
    /// </summary>
    public string TimestampedLogPath => _provider.TimestampedLogPath;

    /// <summary>
    ///     Returns a snapshot of recent error entries recorded during this session.
    /// </summary>
    public IReadOnlyList<string> GetRecentErrors()
    {
        lock (_errorsLock)
        {
            return [.. _recentErrors];
        }
    }

    /// <summary>
    ///     Logs an informational operational message.
    /// </summary>
    public void LogInfo(string message)
    {
        if (!_disposed && !string.IsNullOrWhiteSpace(message))
        {
            _logger.LogInformation("{Message}", message);
        }
    }

    /// <summary>
    ///     Logs a non-fatal warning message.
    /// </summary>
    public void LogWarning(string message)
    {
        if (!_disposed && !string.IsNullOrWhiteSpace(message))
        {
            _logger.LogWarning("{Message}", message);
        }
    }

    /// <summary>
    ///     Logs an error message and records it for diagnostics bundle inclusion.
    /// </summary>
    public void LogError(string message, Exception? exception = null)
    {
        if (_disposed)
        {
            return;
        }

        if (exception != null)
        {
            _logger.LogError(exception, "{Message}", message);
        }
        else
        {
            _logger.LogError("{Message}", message);
        }

        var line = exception != null ? $"{message}: {exception.Message}" : message;
        lock (_errorsLock)
        {
            _recentErrors.Add(line);
            if (_recentErrors.Count > MAX_RECENT_ERRORS)
            {
                _recentErrors.RemoveAt(0);
            }
        }
    }

    /// <summary>
    ///     Flushes buffered log entries to disk.
    /// </summary>
    public void Flush()
    {
        if (!_disposed)
        {
            _provider.Flush(TimeSpan.FromSeconds(2));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _provider.Flush(TimeSpan.FromSeconds(2));
        _provider.Dispose();
    }

    private static LogLevel MapLogLevel(ToolLogLevel level)
    {
        return level switch
        {
            ToolLogLevel.Trace => LogLevel.Trace,
            ToolLogLevel.Debug => LogLevel.Debug,
            ToolLogLevel.Warning => LogLevel.Warning,
            ToolLogLevel.Error => LogLevel.Error,
            _ => LogLevel.Information,
        };
    }
}
