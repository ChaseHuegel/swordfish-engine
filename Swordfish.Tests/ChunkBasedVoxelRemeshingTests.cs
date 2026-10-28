using System;
using System.Collections.Generic;
using System.Diagnostics;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Voxels;
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

        public bool IsCuller(in Voxel voxel, BrickShape shape) => voxel.ID != 0;
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
        brickDb.Bricks[10] = new Brick("lamp", transparent: false, passable: false, meshID: null, BrickShape.Block, textures: default, tags: ["light"]);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var original = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var lamp = new Voxel(_ID: 10, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 8, in original, ref lamp);

        Assert.Contains(new Short3(0, 0, 0), registry.DirtyChunks);
        Assert.Equal(15, voxelObject.Get(8, 8, 8).GetLightLevel());
        Assert.Equal(14, voxelObject.Get(8, 8, 9).GetLightLevel());
        Assert.Equal(14, voxelObject.Get(8, 9, 8).GetLightLevel());
    }

    [Fact]
    public void LocalizedLightingRemoval_DrainsLightAndMarksDirty()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[10] = new Brick("lamp", transparent: false, passable: false, meshID: null, BrickShape.Block, textures: default, tags: ["light"]);

        var lightingService = new VoxelLightingService(brickDb);
        var registry = new VoxelChunkRegistry();
        var voxelObject = new VoxelObject(16);

        var original = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        var lamp = new Voxel(_ID: 10, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

        // Place lamp
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 8, in original, ref lamp);
        registry.DirtyChunks.Clear();

        // Break lamp
        var air = new Voxel(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
        Voxel placedLamp = voxelObject.Get(8, 8, 8);
        lightingService.UpdateLighting(voxelObject, registry, 8, 8, 8, in placedLamp, ref air);

        Assert.Contains(new Short3(0, 0, 0), registry.DirtyChunks);
        Assert.Equal(0, voxelObject.Get(8, 8, 9).GetLightLevel());
        Assert.Equal(0, voxelObject.Get(8, 9, 8).GetLightLevel());
    }

    [Fact]
    public void SingleVoxelEdit_ExecutionTimeUnderOneMillisecond()
    {
        var brickDb = new TestBrickDatabase();
        brickDb.Bricks[1] = new Brick("stone", transparent: false, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);

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
}
