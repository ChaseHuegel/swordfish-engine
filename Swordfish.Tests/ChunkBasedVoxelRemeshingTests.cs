using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Types;
using Swordfish.Library.Types.Shapes;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;
using Xunit;

namespace Swordfish.Tests;

public class ChunkBasedVoxelRemeshingTests
{
    private sealed class TestBrickDatabase : IBrickDatabase
    {
        public readonly Dictionary<ushort, Brick> Bricks = [];

        public Result<Brick> Get(string id) => Result<Brick>.FromFailure((string?)null);

        public Result<Brick> Get(ushort id)
        {
            return Bricks.TryGetValue(id, out Brick brick)
                ? Result<Brick>.FromSuccess(brick)
                : Result<Brick>.FromFailure((string?)null);
        }

        public List<Brick> Get(Func<Brick, bool> predicate) => [];

        public bool IsCuller(in Voxel voxel, BrickShape shape)
        {
            if (voxel.ID == 0 || shape != BrickShape.Block)
            {
                return false;
            }

            if (Bricks.TryGetValue(voxel.ID, out Brick brick))
            {
                return brick.Alpha == BrickAlpha.Opaque && !brick.Passable;
            }

            return true;
        }
    }

    [Fact]
    public void InternalEditOnlyDirtiesCenterChunk()
    {
        var registry = new VoxelChunkRegistry();
        const byte chunkSize = 16;

        // Voxel at (8, 8, 8) is strictly inside chunk (0, 0, 0)
        registry.MarkVoxelModified(8, 8, 8, chunkSize);

        Assert.Single(registry.DirtyChunks);
        Assert.Contains(new Short3(0, 0, 0), registry.DirtyChunks);
    }

    [Fact]
    public void BorderEditDirtiesCenterAndOrthogonalNeighborChunk()
    {
        var registry = new VoxelChunkRegistry();
        const byte chunkSize = 16;

        // Voxel at (0, 8, 8) is on the -X border of chunk (0, 0, 0)
        registry.MarkVoxelModified(0, 8, 8, chunkSize);

        Assert.Equal(2, registry.DirtyChunks.Count);
        Assert.Contains(new Short3(0, 0, 0), registry.DirtyChunks);
        Assert.Contains(new Short3(-1, 0, 0), registry.DirtyChunks);
    }

    [Fact]
    public void CornerBorderEditDirtiesCenterAndThreeOrthogonalNeighbors()
    {
        var registry = new VoxelChunkRegistry();
        const byte chunkSize = 16;

        // Voxel at (0, 0, 0) is on -X, -Y, and -Z borders of chunk (0, 0, 0)
        registry.MarkVoxelModified(0, 0, 0, chunkSize);

        Assert.Equal(4, registry.DirtyChunks.Count);
        Assert.Contains(new Short3(0, 0, 0), registry.DirtyChunks);
        Assert.Contains(new Short3(-1, 0, 0), registry.DirtyChunks);
        Assert.Contains(new Short3(0, -1, 0), registry.DirtyChunks);
        Assert.Contains(new Short3(0, 0, -1), registry.DirtyChunks);
    }

    [Fact]
    public void VoxelObjectHelpersMapCoordinatesCorrectly()
    {
        var voxelObject = new VoxelObject(16);

        Short3 chunk = voxelObject.ToChunkOffset(20, -5, 33);
        Assert.Equal(1, chunk.X);
        Assert.Equal(-1, chunk.Y);
        Assert.Equal(2, chunk.Z);
    }

    [Fact]
    public void SetLightUpdatesLightLevelWithoutModifyingPalette()
    {
        var voxelObject = new VoxelObject(16);
        var voxel = new Voxel(_ID: 5, _ShapeLight: new ShapeLight(default, lightLevel: 0), _Orientation: 0);
        voxelObject.Set(2, 3, 4, voxel);

        Assert.True(voxelObject.TryGetChunk(new Short3(0, 0, 0), out ChunkData chunkData));
        Assert.Equal(1, chunkData.Palette.Count(5));

        voxelObject.SetLight(2, 3, 4, 12);
        Voxel updated = voxelObject.Get(2, 3, 4);

        Assert.Equal(12, updated.GetLightLevel());
        Assert.Equal(5, updated.ID);
        Assert.Equal(1, chunkData.Palette.Count(5));
    }

    [Fact]
    public void LocalizedLightingAddition_PropagatesAndMarksDirty()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[10] = new Brick("lamp", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: ["light"]);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var original = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var lamp = new Voxel(_ID: 10, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 8, in original, ref lamp);

        Assert.Contains(new Short3(0, 0, 0), registry.DirtyChunks);
        Assert.Equal(15, voxelObject.Get(8, 8, 8).GetLightLevel());
        //  Open space is ambient, so the neighbours stay at full light.
        Assert.Equal(15, voxelObject.Get(8, 8, 9).GetLightLevel());
        Assert.Equal(15, voxelObject.Get(8, 9, 8).GetLightLevel());
    }

    [Fact]
    public void LocalizedLightingRemoval_DrainsLightAndMarksDirty()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[1] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);
        brickDb.Bricks[10] = new Brick("lamp", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: ["light"]);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var original = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var stone = new Voxel(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var lamp = new Voxel(_ID: 10, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        // Enclose a room with stone so it is not exposed to sky
        for (int x = 6; x <= 10; x++)
        {
            for (int y = 6; y <= 10; y++)
            {
                voxelObject.Set(x, y, 6, stone);
                voxelObject.Set(x, y, 10, stone);
            }
        }
        for (int z = 6; z <= 10; z++)
        {
            for (int y = 6; y <= 10; y++)
            {
                voxelObject.Set(6, y, z, stone);
                voxelObject.Set(10, y, z, stone);
            }
            for (int x = 6; x <= 10; x++)
            {
                voxelObject.Set(x, 6, z, stone);
                voxelObject.Set(x, 10, z, stone);
            }
        }

        // Place lamp inside room at (8, 8, 8)
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 8, in original, ref lamp);
        registry.DirtyChunks.Clear();

        // Break lamp inside room
        var air = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        Voxel placedLamp = voxelObject.Get(8, 8, 8);
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 8, in placedLamp, ref air);

        Assert.Contains(new Short3(0, 0, 0), registry.DirtyChunks);
        Assert.Equal(0, voxelObject.Get(8, 8, 9).GetLightLevel());
        Assert.Equal(0, voxelObject.Get(8, 9, 8).GetLightLevel());
    }

    [Fact]
    public void DeleteVoxelOnChunkBorder_ReceivesSkyLightAndPropagatesToNeighbors()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[1] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var air = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var stone = new Voxel(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        // Build a tunnel collar around (1, 8, 8) so it only receives light from (0, 8, 8)
        voxelObject.Set(1, 9, 8, stone);
        voxelObject.Set(1, 7, 8, stone);
        voxelObject.Set(1, 8, 9, stone);
        voxelObject.Set(1, 8, 7, stone);
        voxelObject.Set(2, 8, 8, stone);

        // Place stone at chunk border (0, 8, 8)
        lightingService.UpdateLighting(voxelObject, registry, 0, 8, 8, in air, ref stone);
        registry.DirtyChunks.Clear();

        // Delete stone at chunk border (0, 8, 8)
        Voxel placedStone = voxelObject.Get(0, 8, 8);
        lightingService.UpdateLighting(voxelObject, registry, 0, 8, 8, in placedStone, ref air);

        Assert.Equal(15, voxelObject.Get(0, 8, 8).GetLightLevel());
        Assert.Equal(14, voxelObject.Get(1, 8, 8).GetLightLevel());
        Assert.Contains(new Short3(0, 0, 0), registry.DirtyChunks);
        Assert.Contains(new Short3(-1, 0, 0), registry.DirtyChunks);
    }

    [Fact]
    public void DeleteVoxelInsideEnclosedDarkCavity_RemainsDark()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[1] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var air = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var stone = new Voxel(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        // Enclose (8, 8, 8) with stone on all 6 sides to form an enclosed dark cavity
        voxelObject.Set(7, 8, 8, stone);
        voxelObject.Set(9, 8, 8, stone);
        voxelObject.Set(8, 7, 8, stone);
        voxelObject.Set(8, 9, 8, stone);
        voxelObject.Set(8, 8, 7, stone);
        voxelObject.Set(8, 8, 9, stone);
        voxelObject.Set(8, 8, 8, stone);

        registry.DirtyChunks.Clear();

        // Delete center stone voxel (8, 8, 8); all 6 directions hit surrounding stone walls
        Voxel placedStone = voxelObject.Get(8, 8, 8);
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 8, in placedStone, ref air);

        // Not exposed to sky, neighbors are dark -> should remain 0
        Assert.Equal(0, voxelObject.Get(8, 8, 8).GetLightLevel());
        Assert.Contains(new Short3(0, 0, 0), registry.DirtyChunks);
    }

    [Fact]
    public void DeleteVoxelAdjacentToIndoorLight_ReceivesDecayedLight()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[1] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);
        brickDb.Bricks[10] = new Brick("lamp", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: ["light"]);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var air = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var stone = new Voxel(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var lamp = new Voxel(_ID: 10, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        // Enclose the space so it is an indoor room with no direct line of sight to sky
        for (int x = 6; x <= 10; x++)
        {
            for (int y = 6; y <= 10; y++)
            {
                voxelObject.Set(x, y, 6, stone);
                voxelObject.Set(x, y, 12, stone);
            }
        }
        for (int z = 6; z <= 12; z++)
        {
            for (int y = 6; y <= 10; y++)
            {
                voxelObject.Set(6, y, z, stone);
                voxelObject.Set(10, y, z, stone);
            }
            for (int x = 6; x <= 10; x++)
            {
                voxelObject.Set(x, 6, z, stone);
                voxelObject.Set(x, 10, z, stone);
            }
        }

        // Place lamp at (8, 8, 8)
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 8, in air, ref lamp);

        // Place stone at (8, 8, 10), which is 2 blocks away. Air at (8, 8, 9) has light 14.
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 10, in air, ref stone);
        registry.DirtyChunks.Clear();

        // Delete stone at (8, 8, 10)
        Voxel placedStone = voxelObject.Get(8, 8, 10);
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 10, in placedStone, ref air);

        // (8, 8, 10) is internal, not exposed to sky; neighbor at (8, 8, 9) has light 14 -> (8, 8, 10) receives 13
        Assert.Equal(13, voxelObject.Get(8, 8, 10).GetLightLevel());
        Assert.Equal(12, voxelObject.Get(8, 8, 11).GetLightLevel());
    }

    [Fact]
    public void PlaceVoxelOutwardIntoSpace_SeedsAmbientSkyLightOnExposedAirNeighbors()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[1] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var air = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var stone = new Voxel(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        // Place stone block 1 into space at (8, 8, 8)
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 8, in air, ref stone);

        // Exposed air neighbor at (8, 8, 9) should receive ambient sky light level 15
        Assert.Equal(15, voxelObject.Get(8, 8, 9).GetLightLevel());
        Assert.Equal(15, voxelObject.Get(8, 9, 8).GetLightLevel());

        // Build outward into space: place stone block 2 at (8, 8, 9)
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 9, in air, ref stone);

        // Exposed air neighbor at (8, 8, 10) should also receive ambient sky light level 15
        Assert.Equal(15, voxelObject.Get(8, 8, 10).GetLightLevel());
        Assert.Equal(15, voxelObject.Get(8, 9, 9).GetLightLevel());

        // Build further outward: place stone block 3 at (8, 8, 10)
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 10, in air, ref stone);

        // Newly exposed air neighbor at (8, 8, 11) retains full ambient sky light 15
        Assert.Equal(15, voxelObject.Get(8, 8, 11).GetLightLevel());
    }

    [Fact]
    public void MineHoleIntoAsteroidThenBlockOff_CavityBecomesDark()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[1] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var air = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var stone = new Voxel(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        // Build a solid 5x5x5 stone asteroid: x from 6 to 10, y from 6 to 10, z from 6 to 10
        for (int x = 6; x <= 10; x++)
        {
            for (int y = 6; y <= 10; y++)
            {
                for (int z = 6; z <= 10; z++)
                {
                    voxelObject.Set(x, y, z, stone);
                }
            }
        }

        registry.DepthState.Populate(voxelObject, brickDb);

        // Seed ambient light outside asteroid at (5, 8, 8) with light 15
        voxelObject.SetLight(5, 8, 8, 15);

        // Mine a hole from outside into the asteroid at (6, 8, 8), (7, 8, 8), (8, 8, 8)
        Voxel s6 = voxelObject.Get(6, 8, 8);
        lightingService.UpdateLighting(voxelObject, registry, 6, 8, 8, in s6, ref air);

        Voxel s7 = voxelObject.Get(7, 8, 8);
        lightingService.UpdateLighting(voxelObject, registry, 7, 8, 8, in s7, ref air);

        Voxel s8 = voxelObject.Get(8, 8, 8);
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 8, in s8, ref air);

        // Interior voxels receive decayed light from opening, not forced 15
        Assert.True(voxelObject.Get(7, 8, 8).GetLightLevel() < 15);
        Assert.True(voxelObject.Get(8, 8, 8).GetLightLevel() < voxelObject.Get(7, 8, 8).GetLightLevel());

        // Now block off the hole by placing stone back at entrance (6, 8, 8)
        Voxel entranceAir = voxelObject.Get(6, 8, 8);
        lightingService.UpdateLighting(voxelObject, registry, 6, 8, 8, in entranceAir, ref stone);

        // Interior cavity must become completely dark
        Assert.Equal(0, voxelObject.Get(7, 8, 8).GetLightLevel());
        Assert.Equal(0, voxelObject.Get(8, 8, 8).GetLightLevel());

        // Exterior outside entrance retains ambient light 15
        Assert.Equal(15, voxelObject.Get(5, 8, 8).GetLightLevel());
    }

    [Fact]
    public void SealedRoom_PlacingBlockInsideDoesNotSeedLight()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[1] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var air = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var stone = new Voxel(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        // Build a hollow 6x6x6 sealed room: x from 5 to 10, y from 5 to 10, z from 5 to 10
        for (int x = 5; x <= 10; x++)
        {
            for (int y = 5; y <= 10; y++)
            {
                for (int z = 5; z <= 10; z++)
                {
                    bool isWall = x == 5 || x == 10 || y == 5 || y == 10 || z == 5 || z == 10;
                    if (isWall)
                    {
                        voxelObject.Set(x, y, z, stone);
                    }
                }
            }
        }

        registry.DepthState.Populate(voxelObject, brickDb);

        // Place a block inside the sealed room at (7, 7, 7)
        lightingService.UpdateLighting(voxelObject, registry, 7, 7, 7, in air, ref stone);

        // Neighbors inside the sealed room must remain completely dark (0)
        Assert.Equal(0, voxelObject.Get(7, 7, 8).GetLightLevel());
        Assert.Equal(0, voxelObject.Get(7, 8, 7).GetLightLevel());
        Assert.Equal(0, voxelObject.Get(8, 7, 7).GetLightLevel());
        Assert.Equal(0, voxelObject.Get(7, 7, 6).GetLightLevel());
        Assert.Equal(0, voxelObject.Get(7, 6, 7).GetLightLevel());
        Assert.Equal(0, voxelObject.Get(6, 7, 7).GetLightLevel());
    }

    [Fact]
    public void BuildOutwardAcrossChunkBoundary_EveryExposedFaceReceivesAmbient15()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[1] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var air = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var stone = new Voxel(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        // Build outward starting at edge of chunk 0 (coords 14, 15) crossing into chunk 1 (coord 16)
        for (int z = 14; z <= 18; z++)
        {
            lightingService.UpdateLighting(voxelObject, registry, 8, 8, z, in air, ref stone);

            // Exposed neighbor in +Z must receive ambient sky light level 15
            Assert.Equal(15, voxelObject.Get(8, 8, z + 1).GetLightLevel());

            // Exposed orthogonal neighbors (+Y, -Y, +X, -X) must also receive ambient sky light level 15
            Assert.Equal(15, voxelObject.Get(8, 9, z).GetLightLevel());
            Assert.Equal(15, voxelObject.Get(8, 7, z).GetLightLevel());
            Assert.Equal(15, voxelObject.Get(9, 8, z).GetLightLevel());
            Assert.Equal(15, voxelObject.Get(7, 8, z).GetLightLevel());
        }
    }

    [Fact]
    public void SingleVoxelEdit_ExecutionTimeUnderOneMillisecond()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[1] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var air = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var stone = new Voxel(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        // Warm up JIT
        lightingService.UpdateLighting(voxelObject, registry, 5, 5, 5, in air, ref stone);
        registry.DirtyChunks.Clear();

        // Measure time for 100 edits
        var sw = Stopwatch.StartNew();
        const int iterations = 100;
        for (var i = 0; i < iterations; i++)
        {
            var v = stone;
            lightingService.UpdateLighting(voxelObject, registry, 5, 5, 5, in air, ref v);
            registry.DirtyChunks.Clear();
        }
        sw.Stop();

        double averageMsPerEdit = sw.Elapsed.TotalMilliseconds / iterations;
        Assert.True(averageMsPerEdit < 1.0, $"Average edit time {averageMsPerEdit:F4} ms exceeded 1.0 ms budget.");
    }

    [Fact]
    public void Shape_EqualityAndHashCode_WorksAsExpected()
    {
        Shape box1 = new Box3(Vector3.One);
        Shape box2 = new Box3(Vector3.One);
        Shape box3 = new Box3(new Vector3(2, 2, 2));
        Shape sphere = new Sphere(1.0f);

        Assert.Equal(box1, box2);
        Assert.True(box1 == box2);
        Assert.Equal(box1.GetHashCode(), box2.GetHashCode());

        Assert.NotEqual(box1, box3);
        Assert.True(box1 != box3);

        Assert.NotEqual(box1, sphere);
        Assert.True(box1 != sphere);
    }

    [Fact]
    public void ChunkCollision_CombineCollisions_ConcatenatesAllShapes()
    {
        var chunk1 = new ChunkCollisionData(
            [new Box3(Vector3.One)],
            [new Vector3(0, 0, 0)],
            [Quaternion.Identity]
        );

        var chunk2 = new ChunkCollisionData(
            [new Box3(Vector3.One), new Box3(Vector3.One)],
            [new Vector3(16, 0, 0), new Vector3(17, 0, 0)],
            [Quaternion.Identity, Quaternion.Identity]
        );

        CompoundShape combined = VoxelObjectBuilder.CombineCollisions([chunk1, chunk2]);

        Assert.Equal(3, combined.Shapes.Length);
        Assert.Equal(3, combined.Positions.Length);
        Assert.Equal(3, combined.Orientations.Length);
        Assert.Equal(new Vector3(0, 0, 0), combined.Positions[0]);
        Assert.Equal(new Vector3(16, 0, 0), combined.Positions[1]);
        Assert.Equal(new Vector3(17, 0, 0), combined.Positions[2]);
    }

    [Fact]
    public void VoxelChunkRegistry_StoresAndRemovesChunkColliders()
    {
        var registry = new VoxelChunkRegistry();
        var offset = new Short3(1, 0, 0);

        Assert.Empty(registry.ChunkColliders);

        var chunkCollision = new ChunkCollisionData(
            [new Box3(Vector3.One)],
            [new Vector3(16, 0, 0)],
            [Quaternion.Identity]
        );

        registry.ChunkColliders[offset] = chunkCollision;
        Assert.Single(registry.ChunkColliders);
        Assert.True(registry.ChunkColliders.ContainsKey(offset));

        registry.ChunkColliders.Remove(offset);
        Assert.Empty(registry.ChunkColliders);
    }

    private sealed class FakeLineRenderer : ILineRenderer
    {
        public readonly List<Line> CreatedLines = [];

        public Line CreateLine(bool alwaysOnTop = false)
        {
            var line = new Line(this, Vector3.Zero, Vector3.Zero, Vector4.Zero);
            CreatedLines.Add(line);
            return line;
        }

        public Line CreateLine(Vector3 start, Vector3 end, bool alwaysOnTop = false)
        {
            var line = new Line(this, start, end, Vector4.Zero);
            CreatedLines.Add(line);
            return line;
        }

        public Line CreateLine(Vector3 start, Vector3 end, Vector4 color, bool alwaysOnTop = false)
        {
            var line = new Line(this, start, end, color);
            CreatedLines.Add(line);
            return line;
        }

        public void DeleteLine(Line line)
        {
            CreatedLines.Remove(line);
        }
    }

    [Fact]
    public void VoxelDebugGizmoSystem_OverlayTogglesAndTracksNearestChunkAndExtents()
    {
        var store = new DataStore();
        var debugSettings = new DebugSettings();
        var fakeRenderContext = new FakeRenderContext(store);
        var fakeLineRenderer = new FakeLineRenderer();

        using var gizmoSystem = new VoxelDebugGizmoSystem(debugSettings, fakeRenderContext, fakeLineRenderer);
        Assert.Equal(24, fakeLineRenderer.CreatedLines.Count);

        // Initially disabled: all lines should have color zero
        gizmoSystem.Tick(1f / 60f, store);
        for (var i = 0; i < 24; i++)
        {
            Assert.Equal(Vector4.Zero, fakeLineRenderer.CreatedLines[i].Color);
        }

        // Add a voxel entity with two chunks: (0, 0, 0) and (1, 0, 0)
        int voxelEntity = store.Alloc();
        var voxelObject = new VoxelObject(16);
        voxelObject.Set(0, 0, 0, new Voxel(1, 0, 0));
        voxelObject.Set(16, 0, 0, new Voxel(1, 0, 0));
        store.AddOrUpdate(voxelEntity, new VoxelComponent(voxelObject));
        store.AddOrUpdate(voxelEntity, new TransformComponent(Vector3.Zero));

        // Position camera closer to chunk (0, 0, 0)
        CameraEntity camera = fakeRenderContext.MainCamera.Get();
        fakeRenderContext.MainCamera.Set(new CameraEntity(
            camera.Entity,
            camera.ViewFrustum,
            new TransformComponent(new Vector3(2f, 2f, 2f))
        ));

        // Enable overlay. The voxel gizmos stay hidden until their own toggle is on.
        debugSettings.OverlayVisible.Set(true);
        debugSettings.VoxelGizmosVisible.Set(false);
        gizmoSystem.Tick(1f / 60f, store);
        for (var i = 0; i < 24; i++)
        {
            Assert.Equal(Vector4.Zero, fakeLineRenderer.CreatedLines[i].Color);
        }

        debugSettings.VoxelGizmosVisible.Set(true);
        gizmoSystem.Tick(1f / 60f, store);

        // Nearest chunk lines should be active (Cyan)
        for (var i = 0; i < 12; i++)
        {
            Assert.NotEqual(Vector4.Zero, fakeLineRenderer.CreatedLines[i].Color);
        }

        // Chunk lines should span chunk (0, 0, 0) bounds [-0.5, 15.5]
        float minChunkX = float.MaxValue;
        float maxChunkX = float.MinValue;
        for (var i = 0; i < 12; i++)
        {
            Line line = fakeLineRenderer.CreatedLines[i];
            minChunkX = Math.Min(minChunkX, Math.Min(line.Start.X, line.End.X));
            maxChunkX = Math.Max(maxChunkX, Math.Max(line.Start.X, line.End.X));
        }

        Assert.Equal(-0.5f, minChunkX);
        Assert.Equal(15.5f, maxChunkX);

        // Extent lines should span total extents [-0.5, 31.5]
        float minExtX = float.MaxValue;
        float maxExtX = float.MinValue;
        for (var i = 12; i < 24; i++)
        {
            Line line = fakeLineRenderer.CreatedLines[i];
            minExtX = Math.Min(minExtX, Math.Min(line.Start.X, line.End.X));
            maxExtX = Math.Max(maxExtX, Math.Max(line.Start.X, line.End.X));
        }

        Assert.Equal(-0.5f, minExtX);
        Assert.Equal(31.5f, maxExtX);

        // Move camera closer to chunk (1, 0, 0)
        fakeRenderContext.MainCamera.Set(new CameraEntity(
            camera.Entity,
            camera.ViewFrustum,
            new TransformComponent(new Vector3(20f, 2f, 2f))
        ));
        gizmoSystem.Tick(1f / 60f, store);

        minChunkX = float.MaxValue;
        maxChunkX = float.MinValue;
        for (var i = 0; i < 12; i++)
        {
            Line line = fakeLineRenderer.CreatedLines[i];
            minChunkX = Math.Min(minChunkX, Math.Min(line.Start.X, line.End.X));
            maxChunkX = Math.Max(maxChunkX, Math.Max(line.Start.X, line.End.X));
        }

        Assert.Equal(15.5f, minChunkX);
        Assert.Equal(31.5f, maxChunkX);

        // Disable overlay: all lines hidden
        debugSettings.OverlayVisible.Set(false);
        gizmoSystem.Tick(1f / 60f, store);
        for (var i = 0; i < 24; i++)
        {
            Assert.Equal(Vector4.Zero, fakeLineRenderer.CreatedLines[i].Color);
        }
    }

    [Fact]
    public void VoxelEquals_IgnoresLightLevelWhenComparingPredictedAir()
    {
        // Predicted voxel has shape Block and calculated light level 14
        var predicted = new Voxel(0, new ShapeLight(BrickShape.Block, 14), 0);
        // Server echo voxel has shape Block and authoritative light level 0
        var server = new Voxel(0, new ShapeLight(BrickShape.Block, 0), 0);

        Assert.Equal(predicted.GetShapeLight().Shape, server.GetShapeLight().Shape);
        Assert.Equal(predicted.Orientation, server.Orientation);
        Assert.Equal(predicted.ID, server.ID);
    }

    [Fact]
    public void BreakNonCullerVoxel_ReceivesProperLightAndPropagates()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[1] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);
        brickDb.Bricks[2] = new Brick("glass", alpha: BrickAlpha.Blend, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var air = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var stone = new Voxel(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var glass = new Voxel(_ID: 2, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        // Place stone at (8, 8, 8)
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 8, in air, ref stone);
        // Place glass at (8, 8, 9)
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 9, in air, ref glass);

        // Air at (8, 8, 10) is exposed to sky (ambient light level 15)
        voxelObject.SetLight(8, 8, 10, 15);

        // Break glass at (8, 8, 9)
        Voxel currentGlass = voxelObject.Get(8, 8, 9);
        var replacementAir = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 9, in currentGlass, ref replacementAir);

        // The newly empty air cell at (8, 8, 9) is open space, so it is ambient.
        Assert.Equal(15, voxelObject.Get(8, 8, 9).GetLightLevel());
    }

    [Fact]
    public void DepthPrePass_NonCullerBricks_DoNotAddToDepthState()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[1] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);
        brickDb.Bricks[2] = new Brick("glass", alpha: BrickAlpha.Blend, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);

        var depthState = new DepthState();
        var prePass = new DepthPrePass(depthState, brickDb);
        var voxelObject = new VoxelObject(16);

        var glass = new Voxel(_ID: 2, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        voxelObject.Set(8, 8, 8, glass);

        foreach (ChunkData chunk in voxelObject)
        {
            foreach (VoxelSample sample in chunk.GetSampler())
            {
                prePass.Process(sample);
            }
        }

        // Glass is not a culler, so DepthState must remain empty
        Assert.Empty(depthState.XY);
        Assert.Empty(depthState.XZ);
        Assert.Empty(depthState.ZY);

        // Now place stone and process
        var stone = new Voxel(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        voxelObject.Set(8, 8, 8, stone);

        foreach (ChunkData chunk in voxelObject)
        {
            foreach (VoxelSample sample in chunk.GetSampler())
            {
                prePass.Process(sample);
            }
        }

        // Stone is a culler, so DepthState must contain bounds for (8, 8, 8)
        Assert.Single(depthState.XY);
        Assert.Single(depthState.XZ);
        Assert.Single(depthState.ZY);
    }

    [Fact]
    public void ReplaceLampWithCuller_DrainsEmittedLightAndUpdatesDepthState()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[1] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);
        brickDb.Bricks[10] = new Brick("lamp", alpha: BrickAlpha.Blend, passable: false, meshID: null, BrickShape.Block, textures: default, tags: ["light"]);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var air = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var lamp = new Voxel(_ID: 10, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var stone = new Voxel(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        // Place lamp at (8, 8, 8)
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 8, in air, ref lamp);
        Assert.Equal(15, voxelObject.Get(8, 8, 8).GetLightLevel());
        //  Open space is ambient.
        Assert.Equal(15, voxelObject.Get(8, 8, 9).GetLightLevel());

        // Replace lamp directly with stone
        Voxel currentLamp = voxelObject.Get(8, 8, 8);
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 8, in currentLamp, ref stone);

        // Stone must have light level 0 (not 15!)
        Assert.Equal(0, voxelObject.Get(8, 8, 8).GetLightLevel());
        // Stone must be recorded in DepthState
        Assert.True(registry.DepthState.XY.ContainsKey(new WaywardBeyond.Client.Numerics.Int2(8, 8)));
    }

    [Fact]
    public void ReplaceCullerWithLamp_EmitsLightAndRemovesFromDepthState()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[1] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);
        brickDb.Bricks[10] = new Brick("lamp", alpha: BrickAlpha.Blend, passable: false, meshID: null, BrickShape.Block, textures: default, tags: ["light"]);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var air = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var stone = new Voxel(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var lamp = new Voxel(_ID: 10, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        // Place stone at (8, 8, 8)
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 8, in air, ref stone);
        Assert.True(registry.DepthState.XY.ContainsKey(new WaywardBeyond.Client.Numerics.Int2(8, 8)));

        // Replace stone directly with lamp
        Voxel currentStone = voxelObject.Get(8, 8, 8);
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 8, in currentStone, ref lamp);

        // Lamp must emit light 15
        Assert.Equal(15, voxelObject.Get(8, 8, 8).GetLightLevel());
        // Stone must be removed from DepthState
        Assert.False(registry.DepthState.XY.ContainsKey(new WaywardBeyond.Client.Numerics.Int2(8, 8)));
    }

    [Fact]
    public void TunnelEndingAtChunkBorder_ClosingEntranceOverBorder_CavityBecomesDark()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[1] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var air = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var stone = new Voxel(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        // Build asteroid in chunk (0, 0, 0): x from 6 to 10, y from 6 to 10, z from 10 to 15
        for (int x = 6; x <= 10; x++)
        {
            for (int y = 6; y <= 10; y++)
            {
                for (int z = 10; z <= 15; z++)
                {
                    voxelObject.Set(x, y, z, stone);
                }
            }
        }

        registry.DepthState.Populate(voxelObject, brickDb);

        // Dig a tunnel from outside (z = 15 is border) towards the back (z = 10):
        for (int z = 15; z >= 11; z--)
        {
            Voxel current = voxelObject.Get(8, 8, z);
            lightingService.UpdateLighting(voxelObject, registry, 8, 8, z, in current, ref air);
        }

        // Tunnel entrance at (8, 8, 15) was adjacent to non-resident chunk (0, 0, 1), so it received sky light 15.
        Assert.Equal(15, voxelObject.Get(8, 8, 15).GetLightLevel());
        Assert.True(voxelObject.Get(8, 8, 14).GetLightLevel() > 0);

        // Close back of tunnel (place stone at z = 11)
        Voxel air11 = voxelObject.Get(8, 8, 11);
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 11, in air11, ref stone);

        // Now close the front of the tunnel: directly over the border at z = 16 (in neighbor chunk (0, 0, 1))
        Voxel outOfBoundsAir = voxelObject.Get(8, 8, 16);
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 16, in outOfBoundsAir, ref stone);

        // Assert that the interior of the tunnel is dark!
        Assert.Equal(0, voxelObject.Get(8, 8, 15).GetLightLevel());
        Assert.Equal(0, voxelObject.Get(8, 8, 14).GetLightLevel());
        Assert.Equal(0, voxelObject.Get(8, 8, 13).GetLightLevel());
        Assert.Equal(0, voxelObject.Get(8, 8, 12).GetLightLevel());
    }

    [Fact]
    public void WideTunnelWithAmbientExposure_PlacingVoxel_NeighborAtSameDepthStaysLit()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[1] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var air = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var stone = new Voxel(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        // Build asteroid in chunk (0, 0, 0): x from 5 to 11, y from 6 to 10, z from 10 to 15
        for (int x = 5; x <= 11; x++)
        {
            for (int y = 6; y <= 10; y++)
            {
                for (int z = 10; z <= 15; z++)
                {
                    voxelObject.Set(x, y, z, stone);
                }
            }
        }

        registry.DepthState.Populate(voxelObject, brickDb);

        // Dig a 2-wide tunnel at x=7 and x=8 from z=15 down to 10:
        for (int z = 15; z >= 10; z--)
        {
            Voxel current7 = voxelObject.Get(7, 8, z);
            lightingService.UpdateLighting(voxelObject, registry, 7, 8, z, in current7, ref air);

            Voxel current8 = voxelObject.Get(8, 8, z);
            lightingService.UpdateLighting(voxelObject, registry, 8, 8, z, in current8, ref air);
        }

        // Both lanes are lit at z = 12:
        int lightBefore7 = voxelObject.Get(7, 8, 12).GetLightLevel();
        int lightBefore8 = voxelObject.Get(8, 8, 12).GetLightLevel();
        Assert.True(lightBefore7 > 0);
        Assert.True(lightBefore8 > 0);

        // Now place a voxel at (7, 8, 12):
        Voxel air7 = voxelObject.Get(7, 8, 12);
        lightingService.UpdateLighting(voxelObject, registry, 7, 8, 12, in air7, ref stone);

        // The adjacent tunnel neighbor at (8, 8, 12) must STILL be lit!
        Assert.True(voxelObject.Get(8, 8, 12).GetLightLevel() > 0);
    }
}
