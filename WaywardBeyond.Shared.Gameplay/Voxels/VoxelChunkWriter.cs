using System;
using System.Collections.Generic;
using System.Linq;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
///     A lightweight serializable voxel writer: a sparse, chunked buffer that records voxels and can
///     hand back the codegen <see cref="ChunkInfo"/>[] persisted by, and streamed over, the wire. It is the
///     server/headless-facing counterpart to the client's render-coupled <see cref="ChunkInfo"/> store -
///     identical chunk offsets and voxel indexing, so authority colliders, persisted data and the client's
///     mesh build are consistent. It intentionally carries no palette, mesh or lighting state.
/// </summary>
public sealed class VoxelChunkWriter
{
    private readonly byte _chunkSize;
    private readonly int _chunkMask;
    private readonly int _chunkShift;
    private readonly int _chunkShift2;
    private readonly int _voxelsPerChunk;

    private readonly Dictionary<(short X, short Y, short Z), Chunk> _chunks = [];

    public VoxelChunkWriter(byte chunkSize)
    {
        if (chunkSize <= 0 || (chunkSize & (chunkSize - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "Value must be a positive power of 2.");
        }

        _chunkSize = chunkSize;
        _chunkMask = chunkSize - 1;
        _chunkShift = System.Numerics.BitOperations.TrailingZeroCount(chunkSize);
        _chunkShift2 = _chunkShift * 2;
        _voxelsPerChunk = chunkSize * chunkSize * chunkSize;
    }

    public void Set(int x, int y, int z, in Voxel voxel)
    {
        var chunkX = (short)(x >> _chunkShift);
        var chunkY = (short)(y >> _chunkShift);
        var chunkZ = (short)(z >> _chunkShift);
        var key = (chunkX, chunkY, chunkZ);

        if (!_chunks.TryGetValue(key, out Chunk chunk))
        {
            chunk = new Chunk(_chunkSize, new Voxel[_voxelsPerChunk]);
            _chunks[key] = chunk;
        }

        int localX = x < 0 ? x + _chunkSize * Math.Abs((int)chunkX) : x;
        int localY = y < 0 ? y + _chunkSize * Math.Abs((int)chunkY) : y;
        int localZ = z < 0 ? z + _chunkSize * Math.Abs((int)chunkZ) : z;

        int index = (localX & _chunkMask) + ((localY & _chunkMask) << _chunkShift) + ((localZ & _chunkMask) << _chunkShift2);
        chunk.Voxels[index] = voxel;
    }

    public ChunkInfo[] GetChunkInfos()
    {
        return _chunks
            .OrderBy(kvp => kvp.Key, ChunkKeyComparer.Instance)
            .Select(kvp => new ChunkInfo(kvp.Key.X, kvp.Key.Y, kvp.Key.Z, kvp.Value))
            .ToArray();
    }

    private sealed class ChunkKeyComparer : IComparer<(short X, short Y, short Z)>
    {
        public static readonly ChunkKeyComparer Instance = new();

        public int Compare((short X, short Y, short Z) a, (short X, short Y, short Z) b)
        {
            int x = a.X.CompareTo(b.X);
            if (x != 0)
            {
                return x;
            }

            int y = a.Y.CompareTo(b.Y);
            if (y != 0)
            {
                return y;
            }

            return a.Z.CompareTo(b.Z);
        }
    }
}