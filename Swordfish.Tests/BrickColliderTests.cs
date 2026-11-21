using System;
using System.IO;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Types.Shapes;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Voxels;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Pins TOML collider loading in <see cref="BrickDatabase"/> and accurate raycast cell targeting
/// on non-cube brick hitboxes in <see cref="InteractionResolver"/>.
/// </summary>
public class BrickColliderTests
{
    [Fact]
    public void ShippedThrusterLoadsTwoBoxColliders()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Bricks", "assets")));

        IFileParser[] parsers = [new TomlParser<BrickDefinitions>()];
        var parser = new VirtualFileParseService(parsers, vfs);
        var bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs);

        Result<Brick> result = bricks.Get("wb:thruster");
        Assert.True(result.Success, "Shipped wb:thruster must load.");
        Assert.Equal(2, result.Value.Colliders.Length);

        BrickColliderShape baseBox = result.Value.Colliders[0];
        Assert.Equal(ShapeType.Box3, baseBox.Shape.Type);
        Assert.Equal(new Vector3(1f, 1f, 0.1875f), baseBox.Shape.Box3.Extents);
        Assert.Equal(new Vector3(0f, 0f, -0.40625f), baseBox.Offset);

        BrickColliderShape nozzleBox = result.Value.Colliders[1];
        Assert.Equal(ShapeType.Box3, nozzleBox.Shape.Type);
        Assert.Equal(new Vector3(0.75f, 0.75f, 0.8125f), nozzleBox.Shape.Box3.Extents);
        Assert.Equal(new Vector3(0f, 0f, 0.09375f), nozzleBox.Offset);
    }

    [Fact]
    public void TomlCollidersParsePrimitivesAndSkipInvalidEntries()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"wb-collider-test-{Guid.NewGuid():N}");
        string bricksDir = Path.Combine(tempDir, "bricks");
        Directory.CreateDirectory(bricksDir);

        try
        {
            File.WriteAllText(Path.Combine(bricksDir, "mod_parts.toml"), """
                [[Bricks]]
                ID = "mod:antenna"
                Shape = "custom"

                [[Bricks.Colliders]]
                Type = "cylinder"
                Radius = 0.1
                Height = 0.9
                Offset = { X = 0.0, Y = 0.05, Z = 0.0 }

                [[Bricks.Colliders]]
                Type = "sphere"
                Radius = 0.2
                Offset = { X = 0.0, Y = 0.4, Z = 0.0 }

                [[Bricks.Colliders]]
                Type = "box"
                Size = { X = -1.0, Y = 1.0, Z = 1.0 }

                [[Bricks.Colliders]]
                Type = "hull"
                Points = [
                    { X = 0.0, Y = 0.0, Z = 0.0 },
                    { X = 1.0, Y = 0.0, Z = 0.0 },
                    { X = 0.0, Y = 1.0, Z = 0.0 },
                    { X = 1.0, Y = 1.0, Z = 0.0 },
                ]
                """);

            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(tempDir));

            IFileParser[] parsers = [new TomlParser<BrickDefinitions>()];
            var parser = new VirtualFileParseService(parsers, vfs);
            var bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs);

            Result<Brick> result = bricks.Get("mod:antenna");
            Assert.True(result.Success);
            Assert.Equal(2, result.Value.Colliders.Length);
            Assert.Equal(ShapeType.Cylinder, result.Value.Colliders[0].Shape.Type);
            Assert.Equal(ShapeType.Sphere, result.Value.Colliders[1].Shape.Type);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveTargetCellHandlesSlabTopPlacementAndPlateBreaking()
    {
        var voxels = new VoxelObject(4);
        voxels.Set(0, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Slab, 0), 0));

        //  Clicking the top face of a slab (y = 0.0, normal +Y) places in (0, 1, 0), not underneath.
        Int3 placeCell = InteractionResolver.ResolvePlacementCell(
            hitPoint: new Vector3(0f, 0f, 0f),
            hitNormal: Vector3.UnitY,
            structureOrigin: Vector3.Zero,
            structureOrientation: Quaternion.Identity,
            voxelObject: voxels
        );
        Assert.Equal(new Int3(0, 1, 0), placeCell);

        //  Clicking the thin tip of a slope or plate to break stays inside (0, 0, 0).
        Int3 breakCell = InteractionResolver.ResolveHitCell(
            hitPoint: new Vector3(0f, -0.49f, 0.49f),
            hitNormal: Vector3.Normalize(new Vector3(0f, 1f, 1f)),
            structureOrigin: Vector3.Zero,
            structureOrientation: Quaternion.Identity
        );
        Assert.Equal(new Int3(0, 0, 0), breakCell);
    }

    [Fact]
    public void ResolveTargetCellSplitsSlopeDiagonalByHitHalf()
    {
        var voxels = new VoxelObject(4);
        voxels.Set(0, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Slope, 0), 0));
        Vector3 slopeNormal = Vector3.Normalize(new Vector3(0f, 1f, 1f));

        Int3 upperPlace = InteractionResolver.ResolvePlacementCell(
            hitPoint: new Vector3(0f, 0.25f, -0.25f),
            hitNormal: slopeNormal,
            structureOrigin: Vector3.Zero,
            structureOrientation: Quaternion.Identity,
            voxelObject: voxels
        );
        Assert.Equal(new Int3(0, 1, 0), upperPlace);

        Int3 lowerPlace = InteractionResolver.ResolvePlacementCell(
            hitPoint: new Vector3(0f, -0.25f, 0.25f),
            hitNormal: slopeNormal,
            structureOrigin: Vector3.Zero,
            structureOrientation: Quaternion.Identity,
            voxelObject: voxels
        );
        Assert.Equal(new Int3(0, 0, 1), lowerPlace);
    }
}
