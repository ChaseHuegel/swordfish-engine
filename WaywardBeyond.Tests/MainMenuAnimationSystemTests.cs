using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using DryIoc;
using Krill;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.Physics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Meshing;
using WaywardBeyond.Voxels;
using Xunit;
using WaywardBeyondGameState = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

[Collection("ClientGameState")]
public class MainMenuAnimationSystemTests : IDisposable
{
    private readonly GameState _previousState;
    private readonly Container _container;

    public MainMenuAnimationSystemTests()
    {
        _previousState = WaywardBeyondGameState.GameState.Get();
        _container = CreateVoxelContainer();
    }

    public void Dispose()
    {
        WaywardBeyondGameState.GameState.Set(_previousState);
        _container.Dispose();
    }

    [Fact]
    public void SpawnsBlueprintOffsetToRightParentedToCameraWithoutPhysics()
    {
        WaywardBeyondGameState.GameState.Set(GameState.MainMenu);

        var store = new DataStore();
        int camera = AddCamera(store);
        Uuid cameraUuid = store.GetUuid(camera);

        var logger = new RecordingLogger<MainMenuAnimationSystem>();
        var blueprints = new StubBlueprintDatabase(CreateTestModel());
        MainMenuAnimationSystem system = CreateSystem(logger, blueprints);

        system.Tick(0.1f, store);

        Assert.Empty(logger.Errors);
        int spaceship = FindMenuSpaceship(store);
        Assert.True(spaceship >= 0);

        Assert.True(store.TryGet(spaceship, out IdentifierComponent identifier));
        Assert.Equal(MainMenuAnimationSystem.MENU_ENTITY_TAG, identifier.Tag);

        Assert.True(store.TryGet(spaceship, out ChildComponent child));
        Assert.Equal(cameraUuid, child.Parent);
        Assert.True(child.LocalPosition.X > 0f);
        Assert.True(child.LocalPosition.Z < 0f);

        Assert.False(store.TryGet(spaceship, out PhysicsComponent _));
        Assert.False(store.TryGet(spaceship, out ColliderComponent _));
    }

    [Fact]
    public void AppliesGentleBobFloatingMotionEachTick()
    {
        WaywardBeyondGameState.GameState.Set(GameState.MainMenu);

        var store = new DataStore();
        AddCamera(store);

        var logger = new RecordingLogger<MainMenuAnimationSystem>();
        var blueprints = new StubBlueprintDatabase(CreateTestModel());
        MainMenuAnimationSystem system = CreateSystem(logger, blueprints);

        system.Tick(0.25f, store);
        int spaceship = FindMenuSpaceship(store);
        Assert.True(store.TryGet(spaceship, out ChildComponent firstChild));
        Vector3 expectedFirst = MainMenuAnimationSystem.BaseOffset + MainMenuAnimationSystem.ComputeBobOffset(0.25f);
        AssertVectorClose(expectedFirst, firstChild.LocalPosition);

        system.Tick(0.5f, store);
        Assert.True(store.TryGet(spaceship, out ChildComponent secondChild));
        Vector3 expectedSecond = MainMenuAnimationSystem.BaseOffset + MainMenuAnimationSystem.ComputeBobOffset(0.75f);
        AssertVectorClose(expectedSecond, secondChild.LocalPosition);
        Assert.NotEqual(firstChild.LocalPosition, secondChild.LocalPosition);

        //  Verify exact GentleBob formula: x = sin(t) * strength * cos(t), y = sin(t) * strength
        float t = 1.2f;
        Vector3 bob = MainMenuAnimationSystem.ComputeBobOffset(t);
        Assert.Equal(MathF.Sin(t) * MainMenuAnimationSystem.BOB_STRENGTH * MathF.Cos(t), bob.X, precision: 5);
        Assert.Equal(MathF.Sin(t) * MainMenuAnimationSystem.BOB_STRENGTH, bob.Y, precision: 5);
        Assert.Equal(0f, bob.Z, precision: 5);
    }

    [Fact]
    public void FailedBlueprintLoadLogsSingleErrorAndLeavesMenuUsable()
    {
        WaywardBeyondGameState.GameState.Set(GameState.MainMenu);

        var store = new DataStore();
        int camera = AddCamera(store);
        Assert.True(store.TryGet(camera, out TransformComponent initialCamera));

        var logger = new RecordingLogger<MainMenuAnimationSystem>();
        var blueprints = new StubBlueprintDatabase(null);
        MainMenuAnimationSystem system = CreateSystem(logger, blueprints);

        system.Tick(0.1f, store);
        system.Tick(0.1f, store);
        system.Tick(0.1f, store);

        Assert.Single(logger.Errors);
        Assert.Equal(-1, FindMenuSpaceship(store));
        Assert.True(store.TryGet(camera, out TransformComponent afterCamera));
        Assert.NotEqual(initialCamera.Orientation, afterCamera.Orientation);
    }

    [Fact]
    public void LeavingMainMenuDestroysSpaceshipAndAllChildren()
    {
        WaywardBeyondGameState.GameState.Set(GameState.MainMenu);

        var store = new DataStore();
        AddCamera(store);

        var logger = new RecordingLogger<MainMenuAnimationSystem>();
        var blueprints = new StubBlueprintDatabase(CreateTestModel());
        MainMenuAnimationSystem system = CreateSystem(logger, blueprints);

        system.Tick(0.1f, store);
        int spaceship = FindMenuSpaceship(store);
        Assert.True(spaceship >= 0);
        Uuid spaceshipUuid = store.GetUuid(spaceship);
        Assert.True(CountChildren(store, spaceshipUuid) > 0);

        WaywardBeyondGameState.GameState.Set(GameState.Loading);
        system.Tick(0.1f, store);

        Assert.Equal(-1, FindMenuSpaceship(store));
        Assert.Equal(Uuid.Null, store.GetUuid(spaceship));
        Assert.Equal(0, CountChildren(store, spaceshipUuid));
    }

    [Fact]
    public void ReturningToMainMenuResetsCameraAndRespawnsSpaceship()
    {
        WaywardBeyondGameState.GameState.Set(GameState.MainMenu);

        var store = new DataStore();
        int camera = AddCamera(store);

        var logger = new RecordingLogger<MainMenuAnimationSystem>();
        var blueprints = new StubBlueprintDatabase(CreateTestModel());
        MainMenuAnimationSystem system = CreateSystem(logger, blueprints);

        system.Tick(0.1f, store);
        int firstSpaceship = FindMenuSpaceship(store);
        Assert.True(firstSpaceship >= 0);

        //  Simulate entering gameplay and parenting the camera to a moved player
        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        system.Tick(0.1f, store);
        Assert.Equal(-1, FindMenuSpaceship(store));

        store.AddOrUpdate(camera, new ChildComponent(Uuid.NewUuid()));
        store.AddOrUpdate(camera, new TransformComponent(new Vector3(120f, 40f, -80f), Quaternion.CreateFromYawPitchRoll(1f, 0.5f, 0.2f)));

        //  Return to main menu: camera detaches and resets to origin, spaceship respawns
        WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
        system.Tick(0.016f, store);

        Assert.False(store.TryGet(camera, out ChildComponent _));
        Assert.True(store.TryGet(camera, out TransformComponent resetCamera));
        Assert.Equal(Vector3.Zero, resetCamera.Position);

        int respawned = FindMenuSpaceship(store);
        Assert.True(respawned >= 0);
        Assert.True(store.TryGet(respawned, out ChildComponent child));
        Assert.Equal(store.GetUuid(camera), child.Parent);
    }

    [Fact]
    public void BuiltInMainMenuBlueprintDeserializesWithPalette()
    {
        string blueprintPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "WaywardBeyond.Client", "assets", "blueprints", "builtin", "mainMenu.wbp"
        ));
        Assert.True(File.Exists(blueprintPath), $"Expected built-in blueprint at {blueprintPath}");

        string[] knownBricks =
        [
            "wb:caution_panel", "wb:control_buttons", "wb:control_panel", "wb:core",
            "wb:display_console", "wb:display_control", "wb:display_monitor", "wb:glass",
            "wb:grate", "wb:ice", "wb:light", "wb:panel", "wb:porthole", "wb:rock",
            "wb:small_light", "wb:storage", "wb:thruster", "wb:truss", "wb:vent",
        ];
        IBrickRegistry registry = BrickRegistry.FromNames(knownBricks);
        BlueprintData raw = BlueprintData.Deserialize(File.ReadAllBytes(blueprintPath));
        VoxelRegionData encoded = new(raw.SizeX, raw.SizeY, raw.SizeZ, raw.Chunks ?? [], raw.BrickPalette, raw.PaintPalette);
        VoxelRegionData decoded = VoxelCodec.Decode(in encoded, registry);
        VoxelObject voxelObject = VoxelRegionApplier.ToCenteredVoxelObject(in decoded, out _);

        int nonEmptyCount = 0;
        foreach (ChunkData chunk in voxelObject)
        {
            foreach (VoxelSample sample in chunk.GetOccupiedSampler())
            {
                if (sample.Center.ID == 0)
                {
                    continue;
                }

                nonEmptyCount++;
                Assert.True(sample.Center.ID <= knownBricks.Length, $"Unresolved brick id {sample.Center.ID} in mainMenu.wbp");
            }
        }

        Assert.Equal(130, nonEmptyCount);
        Assert.Equal(11, raw.SizeX);
        Assert.Equal(7, raw.SizeY);
        Assert.Equal(16, raw.SizeZ);
    }

    [Fact]
    public void BlueprintDatabaseLoadsMainMenuThroughFileParseServiceWithoutCircularDependency()
    {
        string assetsRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "WaywardBeyond.Client", "assets"
        ));

        string[] knownBricks =
        [
            "wb:caution_panel", "wb:control_buttons", "wb:control_panel", "wb:core",
            "wb:display_console", "wb:display_control", "wb:display_monitor", "wb:glass",
            "wb:grate", "wb:ice", "wb:light", "wb:panel", "wb:porthole", "wb:rock",
            "wb:small_light", "wb:storage", "wb:thruster", "wb:truss", "wb:vent",
        ];

        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(assetsRoot));

        using var di = new Container();
        di.RegisterInstance<ILogger<VirtualFileParseService>>(new RecordingLogger<VirtualFileParseService>());
        di.RegisterInstance<ILogger<BundledBlueprintDatabase>>(new RecordingLogger<BundledBlueprintDatabase>());
        di.RegisterInstance<IBrickRegistry>(BrickRegistry.FromNames(knownBricks));
        di.RegisterInstance(vfs);
        di.Register<IFileParseService, VirtualFileParseService>(Reuse.Singleton);
        di.RegisterBundledBlueprints();
        di.Register<BlueprintDatabase>(Reuse.Singleton);
        di.RegisterMapping<IAssetDatabase<VoxelEntityModel>, BlueprintDatabase>();

        IAssetDatabase<VoxelEntityModel> db = di.Resolve<IAssetDatabase<VoxelEntityModel>>();
        Result<VoxelEntityModel> result = db.Get(MainMenuAnimationSystem.MENU_BLUEPRINT_ID);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Value.VoxelObject);
        Assert.Equal(Vector3.One, result.Value.Scale);

        IBundledBlueprintDatabase bundledDb = di.Resolve<IBundledBlueprintDatabase>();
        Assert.Contains(bundledDb.ListSummaries(), s => s.Id == MainMenuAnimationSystem.MENU_BLUEPRINT_ID && s.BrickCount == 130);
        Assert.True(bundledDb.Get(MainMenuAnimationSystem.MENU_BLUEPRINT_ID).Success);
    }

    [Fact]
    public void BlueprintDatabaseExcludesUserBlueprintsWhenDataRootIsMountedInVfs()
    {
        string assetsRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "WaywardBeyond.Client", "assets"
        ));
        string mainMenuPath = Path.Combine(assetsRoot, "blueprints", "builtin", "mainMenu.wbp");

        string tempDataRoot = Path.Combine(Path.GetTempPath(), $"wb-bpdb-{Guid.NewGuid():N}");
        string userBpDir = Path.Combine(tempDataRoot, "blueprints");
        Directory.CreateDirectory(userBpDir);
        try
        {
            File.Copy(mainMenuPath, Path.Combine(userBpDir, "my-custom-ship.wbp"));

            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(assetsRoot));
            vfs.Mount(new PathInfo(tempDataRoot));

            AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(tempDataRoot));
            var storagePaths = new StoragePaths(new WaywardBeyond.Config.StorageConfig(), appPaths);

            using var di = new Container();
            di.RegisterInstance<ILogger<VirtualFileParseService>>(new RecordingLogger<VirtualFileParseService>());
            di.RegisterInstance<ILogger<BundledBlueprintDatabase>>(new RecordingLogger<BundledBlueprintDatabase>());
            di.RegisterInstance(vfs);
            di.RegisterInstance(storagePaths);
            di.Register<IFileParseService, VirtualFileParseService>(Reuse.Singleton);
            di.RegisterBundledBlueprints();

            IBundledBlueprintDatabase db = di.Resolve<IBundledBlueprintDatabase>();
            Assert.Contains(db.ListSummaries(), s => s.Id == MainMenuAnimationSystem.MENU_BLUEPRINT_ID);
            Assert.DoesNotContain(db.ListSummaries(), s => s.Id == "my-custom-ship");
        }
        finally
        {
            if (Directory.Exists(tempDataRoot))
            {
                Directory.Delete(tempDataRoot, recursive: true);
            }
        }
    }

    private MainMenuAnimationSystem CreateSystem(ILogger<MainMenuAnimationSystem> logger, IAssetDatabase<VoxelEntityModel> blueprints)
    {
        var builder = new VoxelEntityBuilder(
            logger,
            _container.Resolve<VoxelMaterials>(),
            _container.Resolve<VoxelObjectBuilder>(),
            _container.Resolve<IBrickDatabase>(),
            [],
            new VoxelSettings()
        );
        return new MainMenuAnimationSystem(logger, blueprints, builder);
    }

    private static Container CreateVoxelContainer()
    {
        var container = new Container();
        var bricks = new StubBrickDatabase();
        var array = new TextureArray("test", [new Texture("test", [255, 255, 255, 255], 1, 1, mipmaps: false)], mipmaps: false);
        var textures = new PBRTextureArrays(array, array, array, array, array, array);
        var shader = new Shader("test");

        container.RegisterInstance<IBrickDatabase>(bricks);
        var meshDatabase = new StubMeshDatabase();
        container.RegisterInstance<IAssetDatabase<Mesh>>(meshDatabase);
        container.RegisterInstance<IAssetDatabase<VoxelMesh>>(new EngineMeshPreviewDatabase(meshDatabase));
        container.RegisterInstance(textures);
        container.RegisterInstance<ITextureLayerLookup>(new TextureArrayLayerLookup(array));
        container.RegisterInstance(shader);
        container.Register<VoxelMaterials>(Reuse.Singleton);
        container.Register<VoxelObjectBuilder>(Reuse.Singleton);
        container.Register<VoxelLightingService>(Reuse.Singleton);
        container.Register<VoxelObjectProcessor>(Reuse.Scoped);
        container.Register<DepthState>(Reuse.Scoped);
        container.Register<LightingState>(Reuse.Scoped);
        container.Register<MeshState>(Reuse.Scoped);
        container.Register<CollisionState>(Reuse.Scoped);
        container.Register<EntityState>(Reuse.Scoped);
        container.Register<VoxelObjectProcessor.IPass, AmbientLightPass>(Reuse.Scoped);
        container.Register<VoxelObjectProcessor.IPass, LightPropagationPass>(Reuse.Scoped);
        container.Register<VoxelObjectProcessor.IVoxelPass, LightSeedPrePass>(Reuse.Scoped);
        container.Register<VoxelObjectProcessor.ISamplePass, DepthPrePass>(Reuse.Scoped);
        container.Register<VoxelObjectProcessor.ISamplePass, LightPropagationPrePass>(Reuse.Scoped);
        container.Register<MeshPostPass>(Reuse.Scoped);
        container.Register<CollisionPostPass>(Reuse.Scoped);
        container.Register<VoxelObjectProcessor.ISamplePass, CollisionPostPass>(Reuse.Scoped);
        container.Register<VoxelEntityPostPass>(Reuse.Scoped);
        container.Register<VoxelObjectProcessor.ISamplePass, VoxelEntityPostPass>(Reuse.Scoped);
        return container;
    }

    private static VoxelEntityModel CreateTestModel()
    {
        var voxelObject = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        voxelObject.Set(0, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Block, 0), 0));
        return new VoxelEntityModel(Uuid.NewUuid(), Vector3.Zero, Quaternion.Identity, voxelObject);
    }

    private static int AddCamera(DataStore store)
    {
        int camera = store.Alloc();
        store.AddOrUpdate(camera, new CameraComponent());
        store.AddOrUpdate(camera, new TransformComponent(Vector3.Zero, Quaternion.Identity));
        return camera;
    }

    private static int FindMenuSpaceship(DataStore store)
    {
        var finder = new FindSpaceshipAction { Entity = -1 };
        store.Query<VoxelComponent, IdentifierComponent, FindSpaceshipAction>(0f, ref finder);
        return finder.Entity;
    }

    private static int CountChildren(DataStore store, Uuid parentUuid)
    {
        var counter = new CountChildrenAction { ParentUuid = parentUuid };
        store.Query<ChildComponent, CountChildrenAction>(0f, ref counter);
        return counter.Count;
    }

    private static void AssertVectorClose(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 4);
        Assert.Equal(expected.Y, actual.Y, precision: 4);
        Assert.Equal(expected.Z, actual.Z, precision: 4);
    }

    private struct FindSpaceshipAction : IForEach<VoxelComponent, IdentifierComponent>
    {
        public int Entity;

        public void Execute(float delta, DataStore store, int entity, in VoxelComponent voxel, in IdentifierComponent identifier)
        {
            if (identifier.Tag == MainMenuAnimationSystem.MENU_ENTITY_TAG)
            {
                Entity = entity;
            }
        }
    }

    private struct CountChildrenAction : IForEach<ChildComponent>
    {
        public Uuid ParentUuid;
        public int Count;

        public void Execute(float delta, DataStore store, int entity, in ChildComponent child)
        {
            if (child.Parent == ParentUuid)
            {
                Count++;
            }
        }
    }

    private sealed class StubBlueprintDatabase(VoxelEntityModel? model) : IAssetDatabase<VoxelEntityModel>
    {
        private readonly VoxelEntityModel? _model = model;

        public Result<VoxelEntityModel> Get(string id)
        {
            if (_model.HasValue && id == MainMenuAnimationSystem.MENU_BLUEPRINT_ID)
            {
                return Result<VoxelEntityModel>.FromSuccess(_model.Value);
            }

            return Result<VoxelEntityModel>.FromFailure($"Blueprint \"{id}\" not found");
        }
    }

    private sealed class StubBrickDatabase : IBrickDatabase
    {
        private readonly Brick _brick = new("wb:panel", BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, textures: default);

        public Result<Brick> Get(string id) => Result<Brick>.FromSuccess(_brick);
        public Result<Brick> Get(ushort id) => id == 1 ? Result<Brick>.FromSuccess(_brick) : Result<Brick>.FromFailure((string?)null);
        public List<Brick> Get(Func<Brick, bool> predicate) => [];
        public bool IsCuller(in Voxel voxel, BrickShape shape) => voxel.ID != 0 && shape == BrickShape.Block;
    }

    private sealed class StubMeshDatabase : IAssetDatabase<Mesh>
    {
        public Result<Mesh> Get(string id) => Result<Mesh>.FromFailure((string?)null);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public readonly List<string> Errors = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
        }
    }
}
