using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.Library.Types.Shapes;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using Xunit;

namespace WaywardBeyond.Voxels.Tests;

/// <summary>
/// Pins per-shape voxel collision generation, orientation transforms, custom brick colliders,
/// and mixed-shape interior culling in <see cref="VoxelCollision"/>.
/// </summary>
public class VoxelCollisionTests
{
    [Fact]
    public void BuiltInShapesEmitExpectedPrimitives()
    {
        ReadOnlySpan<BrickColliderShape> block = BrickColliders.ForShape(BrickShape.Block);
        Assert.Equal(1, block.Length);
        Assert.Equal(ShapeType.Box3, block[0].Shape.Type);
        Assert.Equal(Vector3.One, block[0].Shape.Box3.Extents);
        Assert.Equal(Vector3.Zero, block[0].Offset);

        ReadOnlySpan<BrickColliderShape> slab = BrickColliders.ForShape(BrickShape.Slab);
        Assert.Equal(1, slab.Length);
        Assert.Equal(new Vector3(1f, 0.5f, 1f), slab[0].Shape.Box3.Extents);
        Assert.Equal(new Vector3(0f, -0.25f, 0f), slab[0].Offset);

        ReadOnlySpan<BrickColliderShape> plate = BrickColliders.ForShape(BrickShape.Plate);
        Assert.Equal(1, plate.Length);
        Assert.Equal(new Vector3(1f, 0.125f, 1f), plate[0].Shape.Box3.Extents);
        Assert.Equal(new Vector3(0f, -0.4375f, 0f), plate[0].Offset);

        ReadOnlySpan<BrickColliderShape> column = BrickColliders.ForShape(BrickShape.Column);
        Assert.Equal(1, column.Length);
        Assert.Equal(new Vector3(0.5f, 1f, 0.5f), column[0].Shape.Box3.Extents);
        Assert.Equal(Vector3.Zero, column[0].Offset);

        ReadOnlySpan<BrickColliderShape> stair = BrickColliders.ForShape(BrickShape.Stair);
        Assert.Equal(2, stair.Length);
        Assert.Equal(new Vector3(1f, 0.5f, 1f), stair[0].Shape.Box3.Extents);
        Assert.Equal(new Vector3(0f, -0.25f, 0f), stair[0].Offset);
        Assert.Equal(new Vector3(1f, 0.5f, 0.5f), stair[1].Shape.Box3.Extents);
        Assert.Equal(new Vector3(0f, 0.25f, -0.25f), stair[1].Offset);

        ReadOnlySpan<BrickColliderShape> slope = BrickColliders.ForShape(BrickShape.Slope);
        Assert.Equal(1, slope.Length);
        Assert.Equal(ShapeType.ConvexHull, slope[0].Shape.Type);
        Assert.Equal(6, slope[0].Shape.ConvexHull!.Count);
    }

    [Fact]
    public void CustomShapeWithoutExplicitCollidersFallsBackToUnitCube()
    {
        var brick = new Brick("mod:custom", BrickAlpha.Opaque, false, "custom.obj", BrickShape.Custom, default);

        ReadOnlySpan<BrickColliderShape> parts = BrickColliders.ForShape(BrickShape.Custom, brick);
        Assert.Equal(1, parts.Length);
        Assert.Equal(ShapeType.Box3, parts[0].Shape.Type);
        Assert.Equal(Vector3.One, parts[0].Shape.Box3.Extents);
    }

    [Fact]
    public void CustomShapeWithExplicitCollidersUsesDeclaredPrimitives()
    {
        BrickColliderShape[] customParts =
        [
            new(new Box3(new Vector3(1f, 1f, 0.1875f)), new Vector3(0f, 0f, -0.40625f), Quaternion.Identity),
            new(new Box3(new Vector3(0.75f, 0.75f, 0.8125f)), new Vector3(0f, 0f, 0.09375f), Quaternion.Identity),
        ];
        var brick = new Brick("wb:thruster", BrickAlpha.Opaque, false, "thruster.obj", BrickShape.Custom, default, colliders: customParts);

        ReadOnlySpan<BrickColliderShape> parts = BrickColliders.ForShape(BrickShape.Custom, brick);
        Assert.Equal(2, parts.Length);
        Assert.Equal(customParts[0], parts[0]);
        Assert.Equal(customParts[1], parts[1]);
    }

    [Fact]
    public void BuildCompoundRotatesOffsetsAndOrientationsByVoxelOrientation()
    {
        var voxels = new VoxelObject(4);
        //  180-degree pitch flips a slab from the bottom half (y = -0.25) to the top half (y = +0.25).
        var flipped = new Orientation(pitch: 2, yaw: 0, roll: 0);
        voxels.Set(1, 2, 3, new Voxel(1, new ShapeLight(BrickShape.Slab, 0), flipped.ToByte()));

        CompoundShape compound = VoxelCollision.BuildCompound(voxels);

        Assert.Single(compound.Shapes);
        Assert.Equal(new Vector3(1f, 0.5f, 1f), compound.Shapes[0].Box3.Extents);
        Assert.InRange(compound.Positions[0].X, 0.999f, 1.001f);
        Assert.InRange(compound.Positions[0].Y, 2.249f, 2.251f);
        Assert.InRange(compound.Positions[0].Z, 2.999f, 3.001f);
        Assert.Equal(flipped.ToQuaternion(), compound.Orientations[0]);
    }

    [Fact]
    public void InteriorBlockIsCulledOnlyWhenAllSixNeighborsAreFullBlocks()
    {
        var voxels = new VoxelObject(8);
        var fullBlock = new Voxel(1, new ShapeLight(BrickShape.Block, 0), 0);

        for (int x = 0; x < 3; x++)
        for (int y = 0; y < 3; y++)
        for (int z = 0; z < 3; z++)
        {
            voxels.Set(x, y, z, fullBlock);
        }

        //  3x3x3 solid cube has 26 surface blocks (merged into fewer boxes) and 1 sealed interior block.
        CompoundShape solidCube = VoxelCollision.BuildCompound(voxels);
        Assert.InRange(solidCube.Shapes.Length, 1, 25);
        Assert.False(ContainsPoint(solidCube, new Vector3(1, 1, 1)));
        for (int x = 0; x < 3; x++)
        for (int y = 0; y < 3; y++)
        for (int z = 0; z < 3; z++)
        {
            if (x == 1 && y == 1 && z == 1)
            {
                continue;
            }

            Assert.True(ContainsPoint(solidCube, new Vector3(x, y, z)), $"Expected surface cell ({x},{y},{z}) to be covered.");
        }

        //  Replacing one face neighbor with a slab exposes the center voxel, so all 27 voxels are covered.
        voxels.Set(1, 2, 1, new Voxel(1, new ShapeLight(BrickShape.Slab, 0), 0));
        CompoundShape exposedCenter = VoxelCollision.BuildCompound(voxels);
        Assert.True(ContainsPoint(exposedCenter, new Vector3(1, 1, 1)));
    }

    [Fact]
    public void CustomColliderBrickDoesNotSealNeighborsInInteriorCulling()
    {
        var bricks = new FakeBrickDatabase();
        var thruster = new Brick(
            "wb:thruster",
            BrickAlpha.Opaque,
            false,
            "thruster.obj",
            BrickShape.Custom,
            default,
            colliders: [new BrickColliderShape(new Box3(new Vector3(0.5f, 0.5f, 0.5f)), Vector3.Zero, Quaternion.Identity)]
        );
        bricks.ById[2] = thruster;

        var voxels = new VoxelObject(8);
        var fullBlock = new Voxel(1, new ShapeLight(BrickShape.Block, 0), 0);
        for (int x = 0; x < 3; x++)
        for (int y = 0; y < 3; y++)
        for (int z = 0; z < 3; z++)
        {
            voxels.Set(x, y, z, fullBlock);
        }

        //  Even if stored with BrickShape.Block, a brick with custom colliders is not a full-cell blocker.
        voxels.Set(1, 2, 1, new Voxel(2, new ShapeLight(BrickShape.Block, 0), 0));
        CompoundShape compound = VoxelCollision.BuildCompound(voxels, bricks);
        Assert.True(ContainsPoint(compound, new Vector3(1, 1, 1)));
    }

    private static bool ContainsPoint(CompoundShape compound, Vector3 point)
    {
        for (var i = 0; i < compound.Shapes.Length; i++)
        {
            if (compound.Shapes[i].Type != ShapeType.Box3)
            {
                continue;
            }

            Vector3 halfExtents = compound.Shapes[i].Box3.Extents * 0.5f;
            Vector3 delta = Vector3.Abs(point - compound.Positions[i]);
            if (delta.X <= halfExtents.X && delta.Y <= halfExtents.Y && delta.Z <= halfExtents.Z)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class FakeBrickDatabase : IBrickDatabase
    {
        public readonly Dictionary<ushort, Brick> ById = [];

        public bool IsCuller(in Voxel voxel, BrickShape shape) => false;

        public Result<Brick> Get(ushort id)
        {
            return ById.TryGetValue(id, out Brick? brick)
                ? Result<Brick>.FromSuccess(brick)
                : Result<Brick>.FromFailure("Not found");
        }

        public Result<Brick> Get(string id) => Result<Brick>.FromFailure("Not found");

        public List<Brick> Get(Func<Brick, bool> predicate) => [];
    }
}
