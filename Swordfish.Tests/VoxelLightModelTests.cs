using System.Collections.Generic;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;
using Xunit;

namespace Swordfish.Tests;

public class VoxelLightModelTests
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

        public List<Brick> Get(System.Func<Brick, bool> predicate) => [];

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

    private static readonly Voxel Air = new(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
    private static readonly Voxel Stone = new(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

    private static TestBrickDatabase CreateBrickDatabase()
    {
        var db = new TestBrickDatabase();
        db.Bricks[1] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);
        return db;
    }

    private static VoxelObjectProcessor CreateProcessor(TestBrickDatabase db, DepthState depthState, LightingState lightingState)
    {
        VoxelObjectProcessor.IPass[] passes =
        [
            new AmbientLightPass(lightingState, depthState, db),
            new LightPropagationPass(lightingState, depthState, db),
        ];
        VoxelObjectProcessor.IVoxelPass[] voxelPasses = [new LightSeedPrePass(db)];
        VoxelObjectProcessor.ISamplePass[] samplePasses =
        [
            new DepthPrePass(depthState, db),
            new LightPropagationPrePass(lightingState, db),
        ];
        return new VoxelObjectProcessor(passes, voxelPasses, samplePasses);
    }

    private static void FullLight(VoxelObject voxelObject, TestBrickDatabase db, DepthState depthState)
    {
        var processor = CreateProcessor(db, depthState, new LightingState());
        processor.ProcessLighting(voxelObject);
    }

    [Fact]
    public void OpenAirAboveFlatFloorKeepsFullAmbientLight()
    {
        var db = CreateBrickDatabase();
        var voxelObject = new VoxelObject(16);
        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
        {
            voxelObject.Set(x, 0, z, Stone);
        }

        var depthState = new DepthState();
        FullLight(voxelObject, db, depthState);

        for (int y = 1; y < 16; y++)
        {
            Assert.Equal(15, voxelObject.Get(8, y, 8).GetLightLevel());
        }
    }

    [Fact]
    public void HoleInAThinFloorKeepsFullAmbientLight()
    {
        var db = CreateBrickDatabase();
        var voxelObject = new VoxelObject(16);
        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
        {
            if (x == 8 && z == 8)
            {
                continue;
            }

            voxelObject.Set(x, 0, z, Stone);
        }

        var depthState = new DepthState();
        FullLight(voxelObject, db, depthState);

        Assert.Equal(15, voxelObject.Get(8, 0, 8).GetLightLevel());
        Assert.Equal(15, voxelObject.Get(8, 1, 8).GetLightLevel());
    }

    [Fact]
    public void SealedRoomStaysDark()
    {
        var db = CreateBrickDatabase();
        var voxelObject = new VoxelObject(16);
        for (int x = 4; x <= 10; x++)
        for (int y = 4; y <= 10; y++)
        for (int z = 4; z <= 10; z++)
        {
            bool isWall = x == 4 || x == 10 || y == 4 || y == 10 || z == 4 || z == 10;
            if (isWall)
            {
                voxelObject.Set(x, y, z, Stone);
            }
        }

        var depthState = new DepthState();
        FullLight(voxelObject, db, depthState);

        Assert.Equal(0, voxelObject.Get(7, 7, 7).GetLightLevel());
        Assert.Equal(15, voxelObject.Get(7, 11, 7).GetLightLevel());
    }

    [Fact]
    public void OneMouthTunnelDecaysAwayFromTheOpening()
    {
        var db = CreateBrickDatabase();
        var voxelObject = new VoxelObject(16);
        for (int x = 6; x <= 10; x++)
        for (int y = 6; y <= 10; y++)
        for (int z = 6; z <= 10; z++)
        {
            voxelObject.Set(x, y, z, Stone);
        }

        for (int x = 6; x <= 8; x++)
        {
            voxelObject.Set(x, 8, 8, Air);
        }

        var depthState = new DepthState();
        FullLight(voxelObject, db, depthState);

        int mouth = voxelObject.Get(5, 8, 8).GetLightLevel();
        int x6 = voxelObject.Get(6, 8, 8).GetLightLevel();
        int x7 = voxelObject.Get(7, 8, 8).GetLightLevel();
        int x8 = voxelObject.Get(8, 8, 8).GetLightLevel();

        Assert.Equal(15, mouth);
        Assert.True(x6 < mouth);
        Assert.True(x7 < x6);
        Assert.True(x8 < x7);
    }

    [Fact]
    public void IncrementalEditMatchesAFullRebuild()
    {
        var db = CreateBrickDatabase();
        var incremental = BuildTwoThickFloor();
        var depthState = new DepthState();
        FullLight(incremental, db, depthState);

        var registry = new VoxelChunkRegistry();
        registry.DepthState.Populate(incremental, db);
        registry.DepthStateInitialized = true;

        var service = new VoxelLightingService(db);
        Voxel top = incremental.Get(8, -1, 8);
        var air = Air;
        service.UpdateLighting(incremental, registry, 8, -1, 8, in top, ref air);

        var rebuilt = BuildTwoThickFloor();
        rebuilt.Set(8, -1, 8, Air);
        FullLight(rebuilt, db, new DepthState());

        foreach (ChunkData chunk in incremental)
        {
            if (chunk.Palette.Only(0))
            {
                continue;
            }

            foreach (VoxelSample sample in chunk.GetSampler())
            {
                int x = sample.Coords.X + sample.ChunkOffset.X;
                int y = sample.Coords.Y + sample.ChunkOffset.Y;
                int z = sample.Coords.Z + sample.ChunkOffset.Z;

                Assert.Equal(rebuilt.Get(x, y, z).GetLightLevel(), incremental.Get(x, y, z).GetLightLevel());
            }
        }
    }

    [Fact]
    public void NewChunkFromAnEditSeedsItsAmbientAir()
    {
        var db = CreateBrickDatabase();
        var voxelObject = new VoxelObject(16);
        voxelObject.Set(8, 8, 8, Stone);

        var registry = new VoxelChunkRegistry();
        registry.DepthState.Populate(voxelObject, db);
        registry.DepthStateInitialized = true;

        var service = new VoxelLightingService(db);
        voxelObject.Set(16, 0, 16, Stone);
        var stone = Stone;
        service.UpdateLighting(voxelObject, registry, 16, 0, 16, in Air, ref stone);

        Assert.True(voxelObject.TryGetChunk(new Short3(1, 0, 1), out _));
        Assert.Equal(15, voxelObject.Get(17, 0, 16).GetLightLevel());
    }

    private static VoxelObject BuildTwoThickFloor()
    {
        var voxelObject = new VoxelObject(16);
        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
        {
            voxelObject.Set(x, -1, z, Stone);
            voxelObject.Set(x, -2, z, Stone);
        }

        //  Make the air above the floor resident so the edit can read it.
        voxelObject.Set(8, 0, 8, Air);
        return voxelObject;
    }
}
