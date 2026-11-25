using System.Buffers.Binary;
using System.Diagnostics;
using Avalonia.Media.Imaging;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Thread-safe bounded LRU cache decoding workspace texture and OBJ mesh files into Avalonia thumbnail bitmaps.
/// </summary>
public static class ThumbnailCache
{
    private const int MAX_ENTRIES = 512;
    private const int NATIVE_DECODE_THRESHOLD = 128;
    private const int SCALED_DECODE_WIDTH = 64;

    private static readonly Lock _syncLock = new();
    private static readonly Dictionary<CacheKey, CacheEntry> _entries = [];
    private static readonly LinkedList<CacheKey> _lruOrder = new();

    /// <summary>
    ///     Returns a cached thumbnail bitmap for the given file path or renders and caches it on demand.
    /// </summary>
    public static Bitmap? GetOrLoad(string? path, int maxDimension = SCALED_DECODE_WIDTH)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        int clampedDimension = Math.Clamp(maxDimension, 16, 512);
        string fullPath;
        long fileSize;
        long lastWriteTicks;
        try
        {
            fullPath = Path.GetFullPath(path.Trim());
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                return null;
            }

            fileSize = info.Length;
            lastWriteTicks = info.LastWriteTimeUtc.Ticks;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed checking thumbnail path '{path}': {ex.Message}");
            return null;
        }

        var key = new CacheKey(fullPath, clampedDimension);

        lock (_syncLock)
        {
            if (_entries.TryGetValue(key, out var cached)
                && cached.FileSize == fileSize
                && cached.LastWriteTicks == lastWriteTicks)
            {
                _lruOrder.Remove(cached.OrderNode);
                _lruOrder.AddLast(cached.OrderNode);
                return cached.Bitmap;
            }
        }

        var decoded = DecodeThumbnail(fullPath, clampedDimension);

        lock (_syncLock)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                if (existing.FileSize == fileSize && existing.LastWriteTicks == lastWriteTicks)
                {
                    decoded?.Dispose();
                    _lruOrder.Remove(existing.OrderNode);
                    _lruOrder.AddLast(existing.OrderNode);
                    return existing.Bitmap;
                }

                _lruOrder.Remove(existing.OrderNode);
                _entries.Remove(key);
            }

            var node = _lruOrder.AddLast(key);
            _entries[key] = new CacheEntry(decoded, fileSize, lastWriteTicks, node);

            while (_entries.Count > MAX_ENTRIES && _lruOrder.First != null)
            {
                var oldestNode = _lruOrder.First;
                _lruOrder.RemoveFirst();
                _entries.Remove(oldestNode.Value);
            }

            return decoded;
        }
    }

    /// <summary>
    ///     Clears all cached thumbnail references.
    /// </summary>
    public static void Clear()
    {
        lock (_syncLock)
        {
            _entries.Clear();
            _lruOrder.Clear();
        }
    }

    private static Bitmap? DecodeThumbnail(string fullPath, int maxDimension)
    {
        try
        {
            if (string.Equals(Path.GetExtension(fullPath), ".obj", StringComparison.OrdinalIgnoreCase))
            {
                return MeshThumbnailRenderer.TryRenderThumbnail(fullPath, maxDimension);
            }

            using var stream = File.OpenRead(fullPath);
            if (maxDimension == SCALED_DECODE_WIDTH && IsSmallPng(stream))
            {
                stream.Position = 0;
                return new Bitmap(stream);
            }

            stream.Position = 0;
            return Bitmap.DecodeToWidth(stream, maxDimension, BitmapInterpolationMode.MediumQuality);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed decoding thumbnail '{fullPath}': {ex.Message}");
            return null;
        }
    }

    private static bool IsSmallPng(Stream stream)
    {
        if (!stream.CanSeek)
        {
            return false;
        }

        Span<byte> header = stackalloc byte[24];
        var totalRead = 0;
        while (totalRead < header.Length)
        {
            var read = stream.Read(header[totalRead..]);
            if (read <= 0)
            {
                return false;
            }

            totalRead += read;
        }

        // Verify 8-byte PNG signature and IHDR chunk header
        if (header[0] != 0x89
            || header[1] != 0x50
            || header[2] != 0x4E
            || header[3] != 0x47
            || header[4] != 0x0D
            || header[5] != 0x0A
            || header[6] != 0x1A
            || header[7] != 0x0A
            || header[12] != (byte)'I'
            || header[13] != (byte)'H'
            || header[14] != (byte)'D'
            || header[15] != (byte)'R')
        {
            return false;
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4));
        return width > 0 && height > 0 && width <= NATIVE_DECODE_THRESHOLD && height <= NATIVE_DECODE_THRESHOLD;
    }

    private readonly record struct CacheKey(string FullPath, int MaxDimension);

    private readonly record struct CacheEntry(
        Bitmap? Bitmap,
        long FileSize,
        long LastWriteTicks,
        LinkedListNode<CacheKey> OrderNode);
}
