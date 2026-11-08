using System;
using System.Collections.Generic;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;
using Xunit;

namespace Swordfish.Tests;

public class VoxelLightEntityTests
{
    private const ushort StoneId = 1;
    private const ushort LampId = 2;
    private const ushort GlassId = 3;

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

            return Bricks.TryGetValue(voxel.ID, out Brick brick)
                && brick.Alpha == BrickAlpha.Opaque
                && !brick.Passable;
        }
    }

    private static TestBrickDatabase CreateBrickDatabase()
    {
        var db = new TestBrickDatabase();
        db.Bricks[StoneId] = new Brick("stone", alpha: BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);
        db.Bricks[LampId] = new Brick("lamp", alpha: BrickAlpha.Blend, passable: false, meshID: null, BrickShape.Block, textures: default, tags: ["light"]);
        db.Bricks[GlassId] = new Brick("glass", alpha: BrickAlpha.Blend, passable: false, meshID: null, BrickShape.Block, textures: default, tags: []);
        return db;
    }

    private static Voxel CreateVoxel(ushort id, int lightLevel)
    {
        return new Voxel(_ID: id, _ShapeLight: new ShapeLight(BrickShape.Block, lightLevel), _Orientation: 0);
    }

    private static void FillNeighbours(VoxelObject voxelObject, Voxel voxel, int x, int y, int z, int openMask = 0)
    {
        (int X, int Y, int Z)[] offsets =
        [
            (-1, 0, 0),
            (1, 0, 0),
            (0, -1, 0),
            (0, 1, 0),
            (0, 0, -1),
            (0, 0, 1),
        ];

        for (var i = 0; i < offsets.Length; i++)
        {
            if ((openMask & (1 << i)) != 0)
            {
                continue;
            }

            (int dx, int dy, int dz) = offsets[i];
            voxelObject.Set(x + dx, y + dy, z + dz, voxel);
        }
    }

    private static int ProcessLamp(TestBrickDatabase db, VoxelObject voxelObject, int x, int y, int z)
    {
        var store = new DataStore();
        int entity = store.Alloc();
        var component = new VoxelComponent(voxelObject);
        var voxelInfo = new VoxelInfo(x, y, z, CreateVoxel(LampId, lightLevel: 15));

        var decorator = new LightEntityDecorator(db);
        decorator.Process(store, parent: 0, entity, component, voxelInfo);

        return store.Has(typeof(LightComponent), entity) ? entity : -1;
    }

    [Fact]
    public void OpenLampCreatesShadowCastingLight()
    {
        TestBrickDatabase db = CreateBrickDatabase();
        var voxelObject = new VoxelObject(16);
        voxelObject.Set(8, 8, 8, CreateVoxel(LampId, lightLevel: 15));

        var store = new DataStore();
        int entity = store.Alloc();
        var component = new VoxelComponent(voxelObject);
        var voxelInfo = new VoxelInfo(8, 8, 8, CreateVoxel(LampId, lightLevel: 15));

        new LightEntityDecorator(db).Process(store, parent: 0, entity, component, voxelInfo);

        Assert.True(store.TryGet(entity, out LightComponent light));
        Assert.Equal(15f, light.Radius);
        Assert.True(light.CastShadows);
    }

    [Fact]
    public void BuriedLampCreatesNoLight()
    {
        TestBrickDatabase db = CreateBrickDatabase();
        var voxelObject = new VoxelObject(16);
        voxelObject.Set(8, 8, 8, CreateVoxel(LampId, lightLevel: 15));
        FillNeighbours(voxelObject, CreateVoxel(StoneId, lightLevel: 0), 8, 8, 8);

        Assert.Equal(-1, ProcessLamp(db, voxelObject, 8, 8, 8));
    }

    [Fact]
    public void LampWithOneOpenFaceCreatesLight()
    {
        TestBrickDatabase db = CreateBrickDatabase();
        var voxelObject = new VoxelObject(16);
        voxelObject.Set(8, 8, 8, CreateVoxel(LampId, lightLevel: 15));
        FillNeighbours(voxelObject, CreateVoxel(StoneId, lightLevel: 0), 8, 8, 8, openMask: 1 << 1);

        Assert.NotEqual(-1, ProcessLamp(db, voxelObject, 8, 8, 8));
    }

    [Fact]
    public void LampBehindGlassCreatesLight()
    {
        TestBrickDatabase db = CreateBrickDatabase();
        var voxelObject = new VoxelObject(16);
        voxelObject.Set(8, 8, 8, CreateVoxel(LampId, lightLevel: 15));
        FillNeighbours(voxelObject, CreateVoxel(StoneId, lightLevel: 0), 8, 8, 8);
        voxelObject.Set(9, 8, 8, CreateVoxel(GlassId, lightLevel: 0));

        Assert.NotEqual(-1, ProcessLamp(db, voxelObject, 8, 8, 8));
    }

    [Fact]
    public void BuryingLampRemovesItsLight()
    {
        TestBrickDatabase db = CreateBrickDatabase();
        var voxelObject = new VoxelObject(16);
        voxelObject.Set(8, 8, 8, CreateVoxel(LampId, lightLevel: 15));

        var store = new DataStore();
        int entity = store.Alloc();
        var component = new VoxelComponent(voxelObject);
        var voxelInfo = new VoxelInfo(8, 8, 8, CreateVoxel(LampId, lightLevel: 15));
        var decorator = new LightEntityDecorator(db);

        decorator.Process(store, parent: 0, entity, component, voxelInfo);
        Assert.True(store.Has(typeof(LightComponent), entity));

        FillNeighbours(voxelObject, CreateVoxel(StoneId, lightLevel: 0), 8, 8, 8);
        decorator.Process(store, parent: 0, entity, component, voxelInfo);

        Assert.False(store.Has(typeof(LightComponent), entity));
    }
}
