using System.Collections.Concurrent;
using System.Text;

namespace Shoal.Logging;

/// <summary>
///     Asynchronous file logger provider that batches normal logs on a background thread,
///     flushes periodically and synchronously on high-severity entries, and mirrors bytes to a shared ring buffer.
/// </summary>
public sealed class AsyncFileLoggerProvider : ILoggerProvider
{
    private readonly LoggingConfig _config;
    private readonly LogRingBuffer? _ringBuffer;
    private readonly bool _ownsRingBuffer;
    private readonly ConcurrentQueue<QueueItem> _queue = new();
    private readonly AutoResetEvent _wakeSignal = new(false);
    private readonly object _streamLock = new();
    private readonly FileStream _timestampedStream;
    private readonly FileStream _latestStream;
    private readonly Thread _writerThread;
    private volatile bool _stopping;
    private volatile bool _disposed;

    /// <summary>
    ///     Gets the full path to the timestamped session log file.
    /// </summary>
    public string TimestampedLogPath { get; }

    /// <summary>
    ///     Gets the full path to the latest.log file.
    /// </summary>
    public string LatestLogPath { get; }

    public AsyncFileLoggerProvider(
        LoggingConfig config,
        string logsDirectory,
        LogRingBuffer? ringBuffer = null,
        bool ownsRingBuffer = false,
        string? sessionStamp = null
    )
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _ringBuffer = ringBuffer;
        _ownsRingBuffer = ownsRingBuffer;

        Directory.CreateDirectory(logsDirectory);

        string stamp = string.IsNullOrWhiteSpace(sessionStamp)
            ? DateTime.Now.ToString("yyyy-MM-dd_HHmm-ss", CultureInfo.InvariantCulture)
            : sessionStamp;

        TimestampedLogPath = ResolveUniqueLogPath(logsDirectory, stamp);
        LatestLogPath = Path.Combine(logsDirectory, "latest.log");

        _timestampedStream = new FileStream(TimestampedLogPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, bufferSize: 16384);
        _latestStream = new FileStream(LatestLogPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, bufferSize: 16384);

        PruneOldLogs(logsDirectory, _config.MaxLogFiles, TimestampedLogPath);

        _writerThread = new Thread(WriterLoop)
        {
            Name = "LogWriter",
            IsBackground = true,
        };
        _writerThread.Start();
    }

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName)
    {
        return new AsyncFileLogger(this, categoryName, _config.MinLevel);
    }

    /// <summary>
    ///     Waits up to <paramref name="timeout"/> for all queued entries to be written and flushed to disk.
    /// </summary>
    public void Flush(TimeSpan timeout)
    {
        if (_disposed)
        {
            return;
        }

        if (_stopping || Thread.CurrentThread.ManagedThreadId == _writerThread.ManagedThreadId)
        {
            DrainDirect();
            return;
        }

        using var barrier = new ManualResetEventSlim(false);
        _queue.Enqueue(QueueItem.ForBarrier(barrier));
        TrySignalWake();

        if (!barrier.Wait(timeout))
        {
            DrainDirect();
        }
    }

    /// <summary>
    ///     Writes a formatted emergency message directly to both log files and flushes immediately.
    /// </summary>
    public void WriteEmergency(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(message.EndsWith('\n') ? message : message + Environment.NewLine);
        long seq = _ringBuffer != null ? _ringBuffer.Append(bytes) : 0;

        List<ManualResetEventSlim>? barriers;
        lock (_streamLock)
        {
            if (_disposed)
            {
                return;
            }

            //  Drain earlier buffered entries first so the emergency line stays in chronological order
            //  and earlier ring-buffer entries are on disk before MarkFlushed advances to seq.
            barriers = DrainQueueLocked();
            WriteAndFlushLocked(bytes, seq);
        }

        SignalBarriers(barriers);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _stopping = true;
        _wakeSignal.Set();

        if (_writerThread.IsAlive && Thread.CurrentThread.ManagedThreadId != _writerThread.ManagedThreadId)
        {
            _writerThread.Join(TimeSpan.FromSeconds(3));
        }

        lock (_streamLock)
        {
            if (_disposed)
            {
                return;
            }

            DrainQueueLocked();
            _disposed = true;

            _timestampedStream.Dispose();
            _latestStream.Dispose();
        }

        if (_ownsRingBuffer)
        {
            _ringBuffer?.Dispose();
        }

        _wakeSignal.Dispose();
    }

    internal void Enqueue(LogLevel logLevel, string formattedEntry)
    {
        if (_disposed)
        {
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(formattedEntry);
        long seq = _ringBuffer != null ? _ringBuffer.Append(bytes) : 0;

        if (_stopping)
        {
            lock (_streamLock)
            {
                if (!_disposed)
                {
                    WriteAndFlushLocked(bytes, seq);
                }
            }

            return;
        }

        _queue.Enqueue(QueueItem.ForLog(bytes, seq));
        TrySignalWake();

        if (logLevel >= _config.SyncFlushMinLevel)
        {
            Flush(TimeSpan.FromSeconds(2));
        }
    }

    private void WriterLoop()
    {
        int waitMs = Math.Max(LoggingConfig.MIN_FLUSH_INTERVAL_MS, _config.FlushIntervalMs);
        while (!_stopping)
        {
            _wakeSignal.WaitOne(waitMs);
            DrainDirect();
        }

        DrainDirect();
    }

    private void DrainDirect()
    {
        List<ManualResetEventSlim>? barriers;
        lock (_streamLock)
        {
            if (_disposed)
            {
                return;
            }

            barriers = DrainQueueLocked();
        }

        SignalBarriers(barriers);
    }

    private void TrySignalWake()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _wakeSignal.Set();
        }
        catch (ObjectDisposedException)
        {
            //  Provider disposed concurrently.
        }
    }

    private static void SignalBarriers(List<ManualResetEventSlim>? barriers)
    {
        if (barriers == null)
        {
            return;
        }

        for (var i = 0; i < barriers.Count; i++)
        {
            try
            {
                barriers[i].Set();
            }
            catch (ObjectDisposedException)
            {
                //  Caller timed out before barrier completed.
            }
        }
    }

    private List<ManualResetEventSlim>? DrainQueueLocked()
    {
        List<ManualResetEventSlim>? barriers = null;
        long highestSeq = 0;
        var wroteAny = false;

        while (_queue.TryDequeue(out QueueItem item))
        {
            if (item.Bytes != null)
            {
                _timestampedStream.Write(item.Bytes, 0, item.Bytes.Length);
                _latestStream.Write(item.Bytes, 0, item.Bytes.Length);
                wroteAny = true;

                if (item.RingSequence > highestSeq)
                {
                    highestSeq = item.RingSequence;
                }
            }

            if (item.Barrier != null)
            {
                barriers ??= [];
                barriers.Add(item.Barrier);
            }
        }

        if (wroteAny || barriers != null)
        {
            _timestampedStream.Flush();
            _latestStream.Flush();

            if (highestSeq > 0)
            {
                _ringBuffer?.MarkFlushed(highestSeq);
            }
        }

        return barriers;
    }

    private void WriteAndFlushLocked(byte[] bytes, long seq)
    {
        _timestampedStream.Write(bytes, 0, bytes.Length);
        _latestStream.Write(bytes, 0, bytes.Length);
        _timestampedStream.Flush();
        _latestStream.Flush();

        if (seq > 0)
        {
            _ringBuffer?.MarkFlushed(seq);
        }
    }

    private static string ResolveUniqueLogPath(string logsDirectory, string stamp)
    {
        string candidate = Path.Combine(logsDirectory, $"{stamp}.log");
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        for (var i = 1; i < 1000; i++)
        {
            string numbered = Path.Combine(logsDirectory, $"{stamp}_{i}.log");
            if (!File.Exists(numbered))
            {
                return numbered;
            }
        }

        return Path.Combine(logsDirectory, $"{stamp}_{Guid.NewGuid():N}.log");
    }

    private static void PruneOldLogs(string logsDirectory, int maxLogFiles, string activeLogPath)
    {
        if (maxLogFiles <= 0)
        {
            return;
        }

        try
        {
            string activeFull = Path.GetFullPath(activeLogPath);
            var files = new DirectoryInfo(logsDirectory)
                .GetFiles("*.log", SearchOption.TopDirectoryOnly)
                .Where(file =>
                    !file.Name.Equals("latest.log", StringComparison.OrdinalIgnoreCase) &&
                    !Path.GetFullPath(file.FullName).Equals(activeFull, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToList();

            int keepExisting = Math.Max(0, maxLogFiles - 1);
            for (int i = keepExisting; i < files.Count; i++)
            {
                try
                {
                    files[i].Delete();
                }
                catch
                {
                    //  Ignore locked or concurrently deleted files.
                }
            }
        }
        catch
        {
            //  Pruning must never fail startup.
        }
    }

    private readonly struct QueueItem(byte[]? bytes, long ringSequence, ManualResetEventSlim? barrier)
    {
        public readonly byte[]? Bytes = bytes;
        public readonly long RingSequence = ringSequence;
        public readonly ManualResetEventSlim? Barrier = barrier;

        public static QueueItem ForLog(byte[] bytes, long ringSequence) => new(bytes, ringSequence, null);
        public static QueueItem ForBarrier(ManualResetEventSlim barrier) => new(null, 0, barrier);
    }
}
