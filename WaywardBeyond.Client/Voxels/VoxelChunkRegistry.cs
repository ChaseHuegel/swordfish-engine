using System.Collections.Generic;
using Swordfish.Graphics;
using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels;

/// <summary>
/// Tracks active chunk ranges and dirty chunk offsets for a voxel structure.
/// </summary>
public sealed class VoxelChunkRegistry
{
    public readonly Dictionary<Short3, MeshRangeHandle> OpaqueRanges = [];
    public readonly Dictionary<Short3, MeshRangeHandle> OpaqueNoShadowRanges = [];
    public readonly Dictionary<Short3, MeshRangeHandle> MaskedRanges = [];
    public readonly Dictionary<Short3, MeshRangeHandle> TransparentRanges = [];
    public readonly Dictionary<Short3, ChunkCollisionData> ChunkColliders = [];
    public readonly List<ChunkEntity> BatchEntities = [];
    public readonly HashSet<Short3> DirtyChunks = [];
    public readonly HashSet<Short3> GeometryDirtyChunks = [];
    public readonly HashSet<Short3> LightDirtyChunks = [];
    public readonly HashSet<Short3> EntityDirtyChunks = [];

    /// <summary>Chunk offsets that currently own spawned voxel child entities.</summary>
    public readonly HashSet<Short3> ChunksWithEntities = [];
    public readonly HashSet<Short3> KnownChunks = [];
    public readonly DepthState DepthState = new();

    /// <summary>The opaque range batch for this structure.</summary>
    public MeshBatch? OpaqueBatch;

    /// <summary>The non-shadow-casting opaque range batch for this structure.</summary>
    public MeshBatch? OpaqueNoShadowBatch;

    /// <summary>The masked range batch for this structure.</summary>
    public MeshBatch? MaskedBatch;

    /// <summary>The transparent range batch for this structure.</summary>
    public MeshBatch? TransparentBatch;

    /// <summary>Indicates whether depth state has been populated for this structure.</summary>
    public bool DepthStateInitialized;

    /// <summary>Marks the chunk containing the coordinates and any orthogonal border neighbors as mesh-only dirty for a face paint edit, and flags the center chunk for voxel entity decorator updates.</summary>
    public void MarkFaceModified(int x, int y, int z, byte chunkSize)
    {
        MarkVoxelModified(x, y, z, chunkSize, lightOnly: true);
        int chunkShift = int.TrailingZeroCount(chunkSize);
        EntityDirtyChunks.Add(new Short3((short)(x >> chunkShift), (short)(y >> chunkShift), (short)(z >> chunkShift)));
    }

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