using System.Collections.Generic;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels;

/// <summary>
/// Tracks active chunk child entities and dirty chunk offsets for a voxel structure.
/// </summary>
public sealed class VoxelChunkRegistry
{
    public readonly Dictionary<Short3, int> OpaqueEntities = [];
    public readonly Dictionary<Short3, int> MaskedEntities = [];
    public readonly Dictionary<Short3, int> TransparentEntities = [];
    public readonly HashSet<Short3> DirtyChunks = [];

    /// <summary>Marks the chunk containing the coordinates and any orthogonal border neighbors as dirty.</summary>
    public void MarkVoxelModified(int x, int y, int z, byte chunkSize)
    {
        int chunkMask = chunkSize - 1;
        int chunkShift = int.TrailingZeroCount(chunkSize);
        var chunk = new Short3((short)(x >> chunkShift), (short)(y >> chunkShift), (short)(z >> chunkShift));
        DirtyChunks.Add(chunk);

        int u = x & chunkMask;
        int v = y & chunkMask;
        int w = z & chunkMask;

        if (u == 0) DirtyChunks.Add(new Short3((short)(chunk.X - 1), chunk.Y, chunk.Z));
        if (u == chunkMask) DirtyChunks.Add(new Short3((short)(chunk.X + 1), chunk.Y, chunk.Z));
        if (v == 0) DirtyChunks.Add(new Short3(chunk.X, (short)(chunk.Y - 1), chunk.Z));
        if (v == chunkMask) DirtyChunks.Add(new Short3(chunk.X, (short)(chunk.Y + 1), chunk.Z));
        if (w == 0) DirtyChunks.Add(new Short3(chunk.X, chunk.Y, (short)(chunk.Z - 1)));
        if (w == chunkMask) DirtyChunks.Add(new Short3(chunk.X, chunk.Y, (short)(chunk.Z + 1)));
    }
}
