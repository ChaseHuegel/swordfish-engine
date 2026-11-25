using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.Library.Types.Shapes;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;

namespace WaywardBeyond.Voxels;

/// <summary>Shared collision-shape builder for voxel structures used by both client and server.</summary>
public static class VoxelCollision
{
    /// <summary>Appends the collision primitives for one chunk, culling interior full blocks and merging exposed full blocks into 3D boxes.</summary>
    public static void AppendChunk(
        ChunkData chunkData,
        IBrickDatabase? brickDatabase,
        List<Shape> shapes,
        List<Vector3> positions,
        List<Quaternion> orientations
    ) {
        if (chunkData.Palette.Only(id: 0))
        {
            return;
        }

        int size = chunkData.Data.Size;
        int shift = BitOperations.TrailingZeroCount(size);
        int shift2 = shift * 2;
        int voxelsPerChunk = size * size * size;
        int wordCount = (voxelsPerChunk + 63) >> 6;

        ulong[]? rented = wordCount > 64 ? ArrayPool<ulong>.Shared.Rent(wordCount) : null;
        Span<ulong> exposedBlocks = rented != null ? rented.AsSpan(0, wordCount) : stackalloc ulong[64];
        exposedBlocks[..wordCount].Clear();

        try
        {
            var anyExposedBlock = false;
            var cache = new BrickLookupCache(brickDatabase);
            foreach (VoxelSample sample in chunkData.GetOccupiedSampler())
            {
                if (sample.Center.ID == 0 || IsSealed(in sample, ref cache))
                {
                    continue;
                }

                Brick? brick = cache.GetBrick(sample.Center.ID);
                BrickShape brickShape = new ShapeLight(sample.Center.ShapeLight).Shape;
                if (BrickColliders.IsFullBlock(brickShape, brick))
                {
                    int idx = sample.VoxelIndex;
                    exposedBlocks[idx >> 6] |= 1UL << (idx & 63);
                    anyExposedBlock = true;
                }
                else
                {
                    AppendShapedVoxel(in sample, brickShape, brick, shapes, positions, orientations);
                }
            }

            if (anyExposedBlock)
            {
                EmitMergedBoxes(chunkData.Coords, size, shift, shift2, exposedBlocks, shapes, positions, orientations);
            }
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<ulong>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Appends the collision primitives for one sampled voxel, skipping interior full-block cells.</summary>
    public static void AppendVoxel(
        in VoxelSample sample,
        IBrickDatabase? brickDatabase,
        List<Shape> shapes,
        List<Vector3> positions,
        List<Quaternion> orientations
    ) {
        var cache = new BrickLookupCache(brickDatabase);
        if (sample.Center.ID == 0 || IsSealed(in sample, ref cache))
        {
            return;
        }

        Brick? brick = cache.GetBrick(sample.Center.ID);
        BrickShape brickShape = new ShapeLight(sample.Center.ShapeLight).Shape;
        AppendShapedVoxel(in sample, brickShape, brick, shapes, positions, orientations);
    }

    /// <summary>Builds the full compound collision shape for a live <see cref="VoxelObject"/>.</summary>
    public static CompoundShape BuildCompound(VoxelObject voxelObject, IBrickDatabase? brickDatabase = null)
    {
        int initialCapacity = voxelObject.ChunkCount * 16;
        var shapes = new List<Shape>(initialCapacity);
        var positions = new List<Vector3>(initialCapacity);
        var orientations = new List<Quaternion>(initialCapacity);

        foreach (ChunkData chunk in voxelObject)
        {
            AppendChunk(chunk, brickDatabase, shapes, positions, orientations);
        }

        if (shapes.Count == 0)
        {
            return new CompoundShape([], [], []);
        }

        return new CompoundShape([.. shapes], [.. positions], [.. orientations]);
    }

    private static bool IsSealed(in VoxelSample sample, ref BrickLookupCache cache)
    {
        return cache.IsFullBlock(in sample.Left) &&
               cache.IsFullBlock(in sample.Right) &&
               cache.IsFullBlock(in sample.Above) &&
               cache.IsFullBlock(in sample.Below) &&
               cache.IsFullBlock(in sample.Ahead) &&
               cache.IsFullBlock(in sample.Behind);
    }

    private static void AppendShapedVoxel(
        in VoxelSample sample,
        BrickShape brickShape,
        Brick? brick,
        List<Shape> shapes,
        List<Vector3> positions,
        List<Quaternion> orientations
    ) {
        ReadOnlySpan<BrickColliderShape> parts = BrickColliders.ForShape(brickShape, brick);

        var origin = new Vector3(
            sample.Coords.X + sample.ChunkOffset.X,
            sample.Coords.Y + sample.ChunkOffset.Y,
            sample.Coords.Z + sample.ChunkOffset.Z
        );

        bool isPlainCube = BrickColliders.IsFullBlock(brickShape, brick);
        Quaternion voxelOrientation = isPlainCube
            ? Quaternion.Identity
            : sample.Center.GetOrientation().ToQuaternion();

        for (var i = 0; i < parts.Length; i++)
        {
            BrickColliderShape part = parts[i];
            Vector3 position = part.Offset == Vector3.Zero
                ? origin
                : origin + Vector3.Transform(part.Offset, voxelOrientation);
            Quaternion orientation = part.Orientation == Quaternion.Identity
                ? voxelOrientation
                : voxelOrientation * part.Orientation;

            shapes.Add(part.Shape);
            positions.Add(position);
            orientations.Add(orientation);
        }
    }

    private static void EmitMergedBoxes(
        Short3 coords,
        int size,
        int shift,
        int shift2,
        Span<ulong> exposedBlocks,
        List<Shape> shapes,
        List<Vector3> positions,
        List<Quaternion> orientations
    ) {
        int worldX = coords.X * size;
        int worldY = coords.Y * size;
        int worldZ = coords.Z * size;

        for (var z = 0; z < size; z++)
        for (var y = 0; y < size; y++)
        {
            int yzOffset = (y << shift) + (z << shift2);
            for (var x = 0; x < size; )
            {
                int idx = x + yzOffset;
                if ((exposedBlocks[idx >> 6] & (1UL << (idx & 63))) == 0)
                {
                    x++;
                    continue;
                }

                (int sx, int sy, int sz) = ConsumeBox(exposedBlocks, size, shift, shift2, x, y, z);
                shapes.Add(new Box3(new Vector3(sx, sy, sz)));
                positions.Add(new Vector3(
                    worldX + x + (sx - 1) * 0.5f,
                    worldY + y + (sy - 1) * 0.5f,
                    worldZ + z + (sz - 1) * 0.5f
                ));
                orientations.Add(Quaternion.Identity);

                x += sx;
            }
        }
    }

    private static (int SizeX, int SizeY, int SizeZ) ConsumeBox(
        Span<ulong> exposedBlocks,
        int size,
        int shift,
        int shift2,
        int x,
        int y,
        int z
    ) {
        int yzOffset = (y << shift) + (z << shift2);
        var sx = 1;
        while (x + sx < size && IsBitSet(exposedBlocks, x + sx + yzOffset))
        {
            sx++;
        }

        var sy = 1;
        int zOffset = z << shift2;
        while (y + sy < size && AllBitsSet(exposedBlocks, x + ((y + sy) << shift) + zOffset, sx))
        {
            sy++;
        }

        var sz = 1;
        while (z + sz < size && AreRowsSet(exposedBlocks, shift, shift2, x, y, z + sz, sx, sy))
        {
            sz++;
        }

        ClearBox(exposedBlocks, shift, shift2, x, y, z, sx, sy, sz);
        return (sx, sy, sz);
    }

    private static bool AreRowsSet(
        Span<ulong> mask,
        int shift,
        int shift2,
        int x,
        int y,
        int z,
        int sx,
        int sy
    ) {
        int sliceZOffset = z << shift2;
        for (var dy = 0; dy < sy; dy++)
        {
            int rowOffset = x + ((y + dy) << shift) + sliceZOffset;
            if (!AllBitsSet(mask, rowOffset, sx))
            {
                return false;
            }
        }

        return true;
    }

    private static void ClearBox(
        Span<ulong> mask,
        int shift,
        int shift2,
        int x,
        int y,
        int z,
        int sx,
        int sy,
        int sz
    ) {
        for (var dz = 0; dz < sz; dz++)
        {
            int sliceZOffset = (z + dz) << shift2;
            for (var dy = 0; dy < sy; dy++)
            {
                int rowOffset = x + ((y + dy) << shift) + sliceZOffset;
                ClearBits(mask, rowOffset, sx);
            }
        }
    }

    private static bool IsBitSet(Span<ulong> mask, int idx) => (mask[idx >> 6] & (1UL << (idx & 63))) != 0;

    private static bool AllBitsSet(Span<ulong> mask, int startIdx, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (!IsBitSet(mask, startIdx + i))
            {
                return false;
            }
        }

        return true;
    }

    private static void ClearBits(Span<ulong> mask, int startIdx, int count)
    {
        for (var i = 0; i < count; i++)
        {
            int idx = startIdx + i;
            mask[idx >> 6] &= ~(1UL << (idx & 63));
        }
    }

    private struct BrickLookupCache(IBrickDatabase? brickDatabase)
    {
        private readonly IBrickDatabase? _brickDatabase = brickDatabase;
        private ushort _lastId;
        private Brick? _lastBrick;
        private bool _lastFullBlock;

        public Brick? GetBrick(ushort id)
        {
            if (id == 0 || _brickDatabase == null)
            {
                return null;
            }

            if (id == _lastId)
            {
                return _lastBrick;
            }

            Result<Brick> result = _brickDatabase.Get(id);
            Brick? brick = result.Success ? result.Value : null;
            _lastId = id;
            _lastBrick = brick;
            _lastFullBlock = BrickColliders.IsFullBlock(BrickShape.Block, brick);
            return brick;
        }

        public bool IsFullBlock(in Voxel voxel)
        {
            if (voxel.ID == 0)
            {
                return false;
            }

            BrickShape shape = new ShapeLight(voxel.ShapeLight).Shape;
            if (shape != BrickShape.Block)
            {
                return false;
            }

            if (_brickDatabase == null)
            {
                return true;
            }

            if (voxel.ID != _lastId)
            {
                GetBrick(voxel.ID);
            }

            return _lastFullBlock;
        }
    }
}
