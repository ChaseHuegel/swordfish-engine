using System.Collections.Generic;
using WaywardBeyond.Client.Voxels.Processing;
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
    public readonly Dictionary<Short3, ChunkCollisionData> ChunkColliders = [];
    public readonly HashSet<Short3> DirtyChunks = [];
    public readonly HashSet<Short3> GeometryDirtyChunks = [];
    public readonly HashSet<Short3> LightDirtyChunks = [];
    public readonly HashSet<Short3> KnownChunks = [];
    public readonly DepthState DepthState = new();

    /// <summary>Indicates whether depth state has been populated for this structure.</summary>
    public bool DepthStateInitialized;

    /// <summary>Marks the chunk containing the coordinates and any orthogonal border neighbors as dirty.</summary>
    public void MarkVoxelModified(int x, int y, int z, byte chunkSize, bool lightOnly = false)
    {
        int chunkMask = chunkSize - 1;
        int chunkShift = int.TrailingZeroCount(chunkSize);
        var chunk = new Short3((short)(x >> chunkShift), (short)(y >> chunkShift), (short)(z >> chunkShift));
        MarkChunk(chunk, lightOnly);

        int u = x & chunkMask;
        int v = y & chunkMask;
        int w = z & chunkMask;

        if (u == 0) MarkChunk(new Short3((short)(chunk.X - 1), chunk.Y, chunk.Z), lightOnly);
        if (u == chunkMask) MarkChunk(new Short3((short)(chunk.X + 1), chunk.Y, chunk.Z), lightOnly);
        if (v == 0) MarkChunk(new Short3(chunk.X, (short)(chunk.Y - 1), chunk.Z), lightOnly);
        if (v == chunkMask) MarkChunk(new Short3(chunk.X, (short)(chunk.Y + 1), chunk.Z), lightOnly);
        if (w == 0) MarkChunk(new Short3(chunk.X, chunk.Y, (short)(chunk.Z - 1)), lightOnly);
        if (w == chunkMask) MarkChunk(new Short3(chunk.X, chunk.Y, (short)(chunk.Z + 1)), lightOnly);
    }

    private void MarkChunk(Short3 chunk, bool lightOnly)
    {
        DirtyChunks.Add(chunk);
        if (lightOnly)
        {
            LightDirtyChunks.Add(chunk);
        }
        else
        {
            GeometryDirtyChunks.Add(chunk);
            LightDirtyChunks.Remove(chunk);
        }
    }
}
