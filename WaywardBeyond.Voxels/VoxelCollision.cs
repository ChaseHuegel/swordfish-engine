using System;
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
    /// <summary>Appends the collision primitives for one sampled voxel, skipping interior full-block cells.</summary>
    public static void AppendVoxel(
        in VoxelSample sample,
        IBrickDatabase? brickDatabase,
        List<Shape> shapes,
        List<Vector3> positions,
        List<Quaternion> orientations
    ) {
        if (sample.Center.ID == 0)
        {
            return;
        }

        //  A voxel is sealed only when all six neighbors occupy their full unit cell.
        if (IsFullBlock(in sample.Left, brickDatabase) &&
            IsFullBlock(in sample.Right, brickDatabase) &&
            IsFullBlock(in sample.Above, brickDatabase) &&
            IsFullBlock(in sample.Below, brickDatabase) &&
            IsFullBlock(in sample.Ahead, brickDatabase) &&
            IsFullBlock(in sample.Behind, brickDatabase))
        {
            return;
        }

        Brick? brick = ResolveBrick(sample.Center.ID, brickDatabase);
        BrickShape brickShape = new ShapeLight(sample.Center.ShapeLight).Shape;
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

    /// <summary>Builds the full compound collision shape for a live <see cref="VoxelObject"/>.</summary>
    public static CompoundShape BuildCompound(VoxelObject voxelObject, IBrickDatabase? brickDatabase = null)
    {
        int initialCapacity = voxelObject.ChunkCount * 64;
        var shapes = new List<Shape>(initialCapacity);
        var positions = new List<Vector3>(initialCapacity);
        var orientations = new List<Quaternion>(initialCapacity);

        foreach (ChunkData chunk in voxelObject)
        {
            if (chunk.Palette.Only(id: 0))
            {
                continue;
            }

            foreach (VoxelSample sample in chunk.GetOccupiedSampler())
            {
                AppendVoxel(in sample, brickDatabase, shapes, positions, orientations);
            }
        }

        if (shapes.Count == 0)
        {
            return new CompoundShape([], [], []);
        }

        return new CompoundShape([.. shapes], [.. positions], [.. orientations]);
    }

    private static bool IsFullBlock(in Voxel voxel, IBrickDatabase? brickDatabase)
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

        Brick? brick = ResolveBrick(voxel.ID, brickDatabase);
        return BrickColliders.IsFullBlock(shape, brick);
    }

    private static Brick? ResolveBrick(ushort id, IBrickDatabase? brickDatabase)
    {
        if (brickDatabase == null)
        {
            return null;
        }

        Result<Brick> result = brickDatabase.Get(id);
        return result.Success ? result.Value : null;
    }
}
