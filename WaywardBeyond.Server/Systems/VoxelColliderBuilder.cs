using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.Library.Types.Shapes;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Server authority collision-shape derivation for voxel structures. Delegates to <see cref="VoxelCollision"/>
/// so authority physics and client prediction share one exact shape, orientation, and interior-culling implementation.
/// </summary>
public static class VoxelColliderBuilder
{
    [ThreadStatic]
    private static ScratchLists? _scratch;

    /// <summary>Builds the collision shape for a live voxel structure in world-local coordinates.</summary>
    public static CompoundShape BuildCollision(VoxelObject voxelObject, IBrickDatabase? brickDatabase = null)
    {
        return VoxelCollision.BuildCompound(voxelObject, brickDatabase);
    }

    /// <summary>Populates the per-chunk collider cache and builds the full compound collision shape.</summary>
    public static CompoundShape BuildCollision(in VoxelWorldComponent world, IBrickDatabase? brickDatabase = null)
    {
        world.ChunkColliders.Clear();
        ScratchLists scratch = GetScratch();

        foreach (ChunkData chunk in world.VoxelObject)
        {
            ChunkColliderData chunkCollider = BuildChunkCollision(chunk, brickDatabase, scratch);
            if (!chunkCollider.IsEmpty)
            {
                world.ChunkColliders[chunk.Coords] = chunkCollider;
            }
        }

        return CombineCollisions(world.ChunkColliders.Values);
    }

    /// <summary>Rebuilds only the chunk(s) affected by an edit at <paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/> and returns the combined compound shape.</summary>
    public static CompoundShape RebuildDirtyChunks(
        in VoxelWorldComponent world,
        int x,
        int y,
        int z,
        IBrickDatabase? brickDatabase = null
    ) {
        VoxelObject voxelObject = world.VoxelObject;
        if (world.ChunkColliders.Count == 0 && voxelObject.ChunkCount > 0)
        {
            return BuildCollision(in world, brickDatabase);
        }

        byte chunkSize = voxelObject.ChunkSize;
        int chunkMask = chunkSize - 1;
        int chunkShift = int.TrailingZeroCount(chunkSize);
        var center = new Short3((short)(x >> chunkShift), (short)(y >> chunkShift), (short)(z >> chunkShift));
        ScratchLists scratch = GetScratch();

        UpdateChunk(voxelObject, world.ChunkColliders, center, brickDatabase, scratch);

        int u = x & chunkMask;
        int v = y & chunkMask;
        int w = z & chunkMask;

        if (u == 0) UpdateChunk(voxelObject, world.ChunkColliders, new Short3((short)(center.X - 1), center.Y, center.Z), brickDatabase, scratch);
        if (u == chunkMask) UpdateChunk(voxelObject, world.ChunkColliders, new Short3((short)(center.X + 1), center.Y, center.Z), brickDatabase, scratch);
        if (v == 0) UpdateChunk(voxelObject, world.ChunkColliders, new Short3(center.X, (short)(center.Y - 1), center.Z), brickDatabase, scratch);
        if (v == chunkMask) UpdateChunk(voxelObject, world.ChunkColliders, new Short3(center.X, (short)(center.Y + 1), center.Z), brickDatabase, scratch);
        if (w == 0) UpdateChunk(voxelObject, world.ChunkColliders, new Short3(center.X, center.Y, (short)(center.Z - 1)), brickDatabase, scratch);
        if (w == chunkMask) UpdateChunk(voxelObject, world.ChunkColliders, new Short3(center.X, center.Y, (short)(center.Z + 1)), brickDatabase, scratch);

        return CombineCollisions(world.ChunkColliders.Values);
    }

    /// <summary>Builds the collision shape for a voxel structure from serialized chunks.</summary>
    public static CompoundShape BuildCollision(ChunkInfo[] chunks, IBrickDatabase? brickDatabase = null)
    {
        if (chunks.Length == 0)
        {
            return new CompoundShape([], [], []);
        }

        byte chunkSize = chunks[0].Chunk.Size;
        if (chunkSize <= 0 || (chunkSize & (chunkSize - 1)) != 0)
        {
            chunkSize = VoxelObject.DEFAULT_CHUNK_EDGE;
        }

        var voxelObject = new VoxelObject(chunkSize, chunks);
        return VoxelCollision.BuildCompound(voxelObject, brickDatabase);
    }

    private static void UpdateChunk(
        VoxelObject voxelObject,
        Dictionary<Short3, ChunkColliderData> cache,
        Short3 chunkCoords,
        IBrickDatabase? brickDatabase,
        ScratchLists scratch
    ) {
        if (!voxelObject.TryGetChunk(chunkCoords, out ChunkData chunk) || chunk.Palette.Only(0))
        {
            cache.Remove(chunkCoords);
            return;
        }

        ChunkColliderData collider = BuildChunkCollision(chunk, brickDatabase, scratch);
        if (collider.IsEmpty)
        {
            cache.Remove(chunkCoords);
        }
        else
        {
            cache[chunkCoords] = collider;
        }
    }

    private static ChunkColliderData BuildChunkCollision(
        ChunkData chunk,
        IBrickDatabase? brickDatabase,
        ScratchLists scratch
    ) {
        scratch.Clear();
        VoxelCollision.AppendChunk(chunk, brickDatabase, scratch.Shapes, scratch.Positions, scratch.Orientations);
        if (scratch.Shapes.Count == 0)
        {
            return default;
        }

        return new ChunkColliderData([.. scratch.Shapes], [.. scratch.Positions], [.. scratch.Orientations]);
    }

    private static CompoundShape CombineCollisions(Dictionary<Short3, ChunkColliderData>.ValueCollection chunks)
    {
        var totalShapes = 0;
        foreach (ChunkColliderData chunk in chunks)
        {
            totalShapes += chunk.Shapes.Length;
        }

        if (totalShapes == 0)
        {
            return new CompoundShape([], [], []);
        }

        var shapes = new Shape[totalShapes];
        var positions = new Vector3[totalShapes];
        var orientations = new Quaternion[totalShapes];

        var offset = 0;
        foreach (ChunkColliderData chunk in chunks)
        {
            int count = chunk.Shapes.Length;
            if (count == 0)
            {
                continue;
            }

            Array.Copy(chunk.Shapes, 0, shapes, offset, count);
            Array.Copy(chunk.Positions, 0, positions, offset, count);
            Array.Copy(chunk.Orientations, 0, orientations, offset, count);
            offset += count;
        }

        return new CompoundShape(shapes, positions, orientations);
    }

    private static ScratchLists GetScratch() => _scratch ??= new ScratchLists();

    private sealed class ScratchLists
    {
        public readonly List<Shape> Shapes = [];
        public readonly List<Vector3> Positions = [];
        public readonly List<Quaternion> Orientations = [];

        public void Clear()
        {
            Shapes.Clear();
            Positions.Clear();
            Orientations.Clear();
        }
    }
}
