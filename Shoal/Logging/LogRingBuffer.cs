using System.IO.MemoryMappedFiles;

namespace Shoal.Logging;

/// <summary>
///     Cross-platform file-backed circular UTF-8 byte buffer shared between a child process and its crash supervisor.
/// </summary>
public sealed class LogRingBuffer : IDisposable
{
    public const int DEFAULT_CAPACITY = 256 * 1024;
    private const int HEADER_SIZE = 24;
    private const int OFFSET_CAPACITY = 0;
    private const int OFFSET_WRITE_SEQ = 8;
    private const int OFFSET_FLUSHED_SEQ = 16;

    private readonly object _writeLock = new();
    private readonly FileStream _fileStream;
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly int _capacity;
    private bool _disposed;

    private LogRingBuffer(FileStream fileStream, MemoryMappedFile mmf, MemoryMappedViewAccessor accessor, int capacity)
    {
        _fileStream = fileStream;
        _mmf = mmf;
        _accessor = accessor;
        _capacity = capacity;
    }

    /// <summary>
    ///     Gets the usable payload capacity in bytes.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    ///     Creates or opens a ring buffer backing file for writing.
    /// </summary>
    public static LogRingBuffer CreateOrOpenWriter(string path, int capacity = DEFAULT_CAPACITY)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Ring buffer capacity must be positive.");
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        long totalSize = HEADER_SIZE + capacity;
        FileStream? stream = null;
        MemoryMappedFile? mmf = null;
        MemoryMappedViewAccessor? accessor = null;
        try
        {
            stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
            stream.SetLength(totalSize);

            mmf = MemoryMappedFile.CreateFromFile(stream, mapName: null, totalSize, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: true);
            accessor = mmf.CreateViewAccessor(0, totalSize, MemoryMappedFileAccess.ReadWrite);

            accessor.Write(OFFSET_CAPACITY, (long)capacity);
            accessor.Write(OFFSET_WRITE_SEQ, 0L);
            accessor.Write(OFFSET_FLUSHED_SEQ, 0L);
            accessor.Flush();

            return new LogRingBuffer(stream, mmf, accessor, capacity);
        }
        catch
        {
            accessor?.Dispose();
            mmf?.Dispose();
            stream?.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Opens an existing ring buffer backing file for reading after process exit.
    /// </summary>
    public static LogRingBuffer? TryOpenReader(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length <= HEADER_SIZE)
            {
                stream.Dispose();
                return null;
            }

            long totalSize = stream.Length;
            var mmf = MemoryMappedFile.CreateFromFile(stream, mapName: null, totalSize, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
            var accessor = mmf.CreateViewAccessor(0, totalSize, MemoryMappedFileAccess.Read);

            long capacity = accessor.ReadInt64(OFFSET_CAPACITY);
            if (capacity <= 0 || HEADER_SIZE + capacity > totalSize)
            {
                accessor.Dispose();
                mmf.Dispose();
                stream.Dispose();
                return null;
            }

            return new LogRingBuffer(stream, mmf, accessor, (int)capacity);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Appends UTF-8 log bytes to the ring buffer and returns the new write sequence position.
    /// </summary>
    public long Append(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return ReadWriteSequence();
        }

        lock (_writeLock)
        {
            if (_disposed)
            {
                return 0;
            }

            long writeSeq = _accessor.ReadInt64(OFFSET_WRITE_SEQ);
            ReadOnlySpan<byte> slice = data.Length > _capacity ? data[^_capacity..] : data;

            //  If the single entry exceeded capacity, advance writeSeq past the skipped prefix.
            if (data.Length > _capacity)
            {
                writeSeq += data.Length - _capacity;
            }

            int startOffset = (int)(writeSeq % _capacity);
            int firstChunk = Math.Min(slice.Length, _capacity - startOffset);

            WriteSpan(HEADER_SIZE + startOffset, slice[..firstChunk]);
            if (firstChunk < slice.Length)
            {
                WriteSpan(HEADER_SIZE, slice[firstChunk..]);
            }

            long nextSeq = writeSeq + slice.Length;
            Thread.MemoryBarrier();
            _accessor.Write(OFFSET_WRITE_SEQ, nextSeq);
            return nextSeq;
        }
    }

    /// <summary>
    ///     Records that all bytes up to <paramref name="sequence"/> have been flushed to disk.
    /// </summary>
    public void MarkFlushed(long sequence)
    {
        lock (_writeLock)
        {
            if (_disposed)
            {
                return;
            }

            long current = _accessor.ReadInt64(OFFSET_FLUSHED_SEQ);
            if (sequence > current)
            {
                _accessor.Write(OFFSET_FLUSHED_SEQ, sequence);
            }
        }
    }

    /// <summary>
    ///     Reads any bytes written to the ring buffer that were not yet marked flushed.
    /// </summary>
    public byte[] ReadUnflushedBytes()
    {
        if (_disposed)
        {
            return [];
        }

        long writeSeq = _accessor.ReadInt64(OFFSET_WRITE_SEQ);
        long flushedSeq = _accessor.ReadInt64(OFFSET_FLUSHED_SEQ);
        if (writeSeq <= flushedSeq)
        {
            return [];
        }

        long unread = writeSeq - flushedSeq;
        long startSeq = flushedSeq;
        if (unread > _capacity)
        {
            startSeq = writeSeq - _capacity;
            unread = _capacity;
        }

        int count = (int)unread;
        var result = new byte[count];
        int startOffset = (int)(startSeq % _capacity);
        int firstChunk = Math.Min(count, _capacity - startOffset);

        _accessor.ReadArray(HEADER_SIZE + startOffset, result, 0, firstChunk);
        if (firstChunk < count)
        {
            _accessor.ReadArray(HEADER_SIZE, result, firstChunk, count - firstChunk);
        }

        return result;
    }

    /// <summary>
    ///     Reads the current write sequence number.
    /// </summary>
    public long ReadWriteSequence()
    {
        return _disposed ? 0 : _accessor.ReadInt64(OFFSET_WRITE_SEQ);
    }

    /// <summary>
    ///     Reads the current flushed sequence number.
    /// </summary>
    public long ReadFlushedSequence()
    {
        return _disposed ? 0 : _accessor.ReadInt64(OFFSET_FLUSHED_SEQ);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_writeLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _accessor.Dispose();
            _mmf.Dispose();
            _fileStream.Dispose();
        }
    }

    private void WriteSpan(long position, ReadOnlySpan<byte> source)
    {
        unsafe
        {
            byte* basePtr = null;
            _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref basePtr);
            try
            {
                var destination = new Span<byte>(basePtr + position, source.Length);
                source.CopyTo(destination);
            }
            finally
            {
                _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }
    }
}
