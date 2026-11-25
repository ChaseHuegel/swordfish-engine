using System.Diagnostics;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Watches workspace directories for external file changes with noise filtering and burst debouncing.
/// </summary>
public sealed class WorkspaceFileWatcher : IDisposable
{
    private static readonly string[] IGNORED_DIRECTORY_NAMES =
    [
        ".git",
        "bin",
        "obj",
        ".vs",
        ".idea",
        ".assetstudio",
    ];

    private static readonly string[] IGNORED_FILE_SUFFIXES =
    [
        ".tmp",
        ".bak",
        ".swp",
        ".swx",
        ".partial",
        "~",
    ];

    private readonly Lock _syncLock = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly HashSet<string> _pendingPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _debounceTimer;
    private readonly int _debounceMilliseconds;
    private bool _disposed;

    /// <summary>
    ///     Initializes a debounced workspace filesystem watcher.
    /// </summary>
    public WorkspaceFileWatcher(int debounceMilliseconds = 300)
    {
        _debounceMilliseconds = Math.Max(10, debounceMilliseconds);
        _debounceTimer = new Timer(OnDebounceTimerElapsed, state: null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    ///     Raised with deduplicated changed file paths after a quiet debounce window elapses.
    /// </summary>
    public event Action<IReadOnlyList<string>>? FilesChanged;

    /// <summary>
    ///     Replaces any active watchers and starts monitoring <paramref name="directories"/> recursively.
    /// </summary>
    public void Watch(IReadOnlyList<string> directories)
    {
        Stop();

        if (directories == null || directories.Count == 0)
        {
            return;
        }

        var uniqueDirs = NormalizeDirectories(directories);
        using (_syncLock.EnterScope())
        {
            if (_disposed)
            {
                return;
            }

            for (var i = 0; i < uniqueDirs.Count; i++)
            {
                var dir = uniqueDirs[i];
                try
                {
                    if (!Directory.Exists(dir))
                    {
                        continue;
                    }

                    var watcher = new FileSystemWatcher(dir)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName
                            | NotifyFilters.LastWrite
                            | NotifyFilters.Size
                            | NotifyFilters.CreationTime,
                    };

                    watcher.Changed += OnFileSystemEvent;
                    watcher.Created += OnFileSystemEvent;
                    watcher.Deleted += OnFileSystemEvent;
                    watcher.Renamed += OnRenamedEvent;
                    watcher.Error += OnWatcherError;
                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Failed starting filesystem watcher on '{dir}': {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    ///     Stops all active directory watchers and discards pending debounced notifications.
    /// </summary>
    public void Stop()
    {
        List<FileSystemWatcher> toDispose;
        using (_syncLock.EnterScope())
        {
            toDispose = [.. _watchers];
            _watchers.Clear();
            _pendingPaths.Clear();
            if (!_disposed)
            {
                try
                {
                    _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Failed resetting file watcher timer: {ex.Message}");
                }
            }
        }

        DisposeWatchers(toDispose);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        List<FileSystemWatcher> toDispose;
        using (_syncLock.EnterScope())
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            toDispose = [.. _watchers];
            _watchers.Clear();
            _pendingPaths.Clear();
        }

        DisposeWatchers(toDispose);
        _debounceTimer.Dispose();
    }

    internal static bool ShouldIgnorePath(string? fullPath, string? watchedRoot = null)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return true;
        }

        var trimmedPath = fullPath.TrimEnd('/', '\\');
        var fileName = Path.GetFileName(trimmedPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return true;
        }

        if (fileName.StartsWith('.') || fileName.Contains(".tmp.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (var i = 0; i < IGNORED_FILE_SUFFIXES.Length; i++)
        {
            if (fileName.EndsWith(IGNORED_FILE_SUFFIXES[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var pathForDirCheck = fullPath;
        if (!string.IsNullOrWhiteSpace(watchedRoot))
        {
            try
            {
                pathForDirCheck = Path.GetRelativePath(watchedRoot, fullPath);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed computing relative watched path for '{fullPath}' under '{watchedRoot}': {ex.Message}");
            }
        }

        var normalized = pathForDirCheck.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment == "." || segment == "..")
            {
                continue;
            }

            if (i < segments.Length - 1 && segment.StartsWith('.'))
            {
                return true;
            }

            for (var j = 0; j < IGNORED_DIRECTORY_NAMES.Length; j++)
            {
                if (string.Equals(segment, IGNORED_DIRECTORY_NAMES[j], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        var watchedRoot = sender is FileSystemWatcher watcher ? watcher.Path : null;
        QueueChangedPath(e.FullPath, watchedRoot);
    }

    private void OnRenamedEvent(object sender, RenamedEventArgs e)
    {
        var watchedRoot = sender is FileSystemWatcher watcher ? watcher.Path : null;
        QueueChangedPath(e.OldFullPath, watchedRoot);
        QueueChangedPath(e.FullPath, watchedRoot);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        Trace.TraceWarning($"Workspace filesystem watcher error: {ex?.Message ?? "unknown error"}");
    }

    private void QueueChangedPath(string? fullPath, string? watchedRoot)
    {
        if (ShouldIgnorePath(fullPath, watchedRoot))
        {
            return;
        }

        string normalized;
        try
        {
            normalized = Path.GetFullPath(fullPath!);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Ignoring invalid watched path '{fullPath}': {ex.Message}");
            return;
        }

        using (_syncLock.EnterScope())
        {
            if (_disposed)
            {
                return;
            }

            _pendingPaths.Add(normalized);
            try
            {
                _debounceTimer.Change(_debounceMilliseconds, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed scheduling watcher debounce timer: {ex.Message}");
            }
        }
    }

    private void OnDebounceTimerElapsed(object? state)
    {
        List<string> batch;
        using (_syncLock.EnterScope())
        {
            if (_disposed || _pendingPaths.Count == 0)
            {
                return;
            }

            batch = [.. _pendingPaths];
            _pendingPaths.Clear();
        }

        batch.Sort(StringComparer.OrdinalIgnoreCase);

        try
        {
            FilesChanged?.Invoke(batch);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"WorkspaceFileWatcher subscriber threw an exception: {ex.Message}");
        }
    }

    private void DisposeWatchers(List<FileSystemWatcher> watchers)
    {
        for (var i = 0; i < watchers.Count; i++)
        {
            var watcher = watchers[i];
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= OnFileSystemEvent;
                watcher.Created -= OnFileSystemEvent;
                watcher.Deleted -= OnFileSystemEvent;
                watcher.Renamed -= OnRenamedEvent;
                watcher.Error -= OnWatcherError;
                watcher.Dispose();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed disposing filesystem watcher: {ex.Message}");
            }
        }
    }

    private static List<string> NormalizeDirectories(IReadOnlyList<string> directories)
    {
        var normalized = new List<string>(directories.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < directories.Count; i++)
        {
            var raw = directories[i];
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            try
            {
                var full = Path.GetFullPath(raw).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (Directory.Exists(full) && seen.Add(full))
                {
                    normalized.Add(full);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Ignoring invalid watch directory '{raw}': {ex.Message}");
            }
        }

        return normalized;
    }
}
