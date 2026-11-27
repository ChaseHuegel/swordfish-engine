#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Events;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using Swordfish.Tests;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Items;
using WaywardBeyond.Meshing;
using WaywardBeyond.Voxels;
using Xunit;
using GameState = WaywardBeyond.Client.GameState;
using WaywardBeyondGameState = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

/// <summary>Tests for first-person view model visual resolution, PBR options, and mesh lifetime.</summary>
[Collection("ClientGameState")]
public class PlayerViewModelTests
{
    private const string TEXTURE_A = "test:tex_a";
    private const string TEXTURE_B = "test:tex_b";

    private sealed class TestBrickRegistry : IBrickRegistry
    {
        private readonly Dictionary<string, ushort> _ids = new(StringComparer.Ordinal);

        public ushort Bind(string id)
        {
            ushort value = (ushort)(_ids.Count + 1);
            _ids[id] = value;
            return value;
        }

        public ushort Id(string name) => _ids.TryGetValue(name, out ushort id) ? id : (ushort)0;

        public string? Name(ushort id)
        {
            foreach ((string name, ushort value) in _ids)
            {
                if (value == id)
                {
                    return name;
                }
            }

            return null;
        }
    }

    private sealed class TestBrickDatabase : IBrickDatabase
    {
        private readonly Dictionary<ushort, Brick> _bricks = [];

        public void Add(ushort id, Brick brick) => _bricks[id] = brick;

        public Result<Brick> Get(string id)
        {
            foreach (Brick brick in _bricks.Values)
            {
                if (brick.ID == id)
                {
                    return Result<Brick>.FromSuccess(brick);
                }
            }

            return Result<Brick>.FromFailure((string?)null);
        }

        public Result<Brick> Get(ushort id) =>
            _bricks.TryGetValue(id, out Brick? brick)
                ? Result<Brick>.FromSuccess(brick)
                : Result<Brick>.FromFailure((string?)null);

        public List<Brick> Get(Func<Brick, bool> predicate) => [.. _bricks.Values.Where(predicate)];

        public bool IsCuller(in Voxel voxel, BrickShape shape) => false;
    }

    private sealed class TestAssetDatabase<T> : IAssetDatabase<T>
    {
        public readonly Dictionary<string, T> Assets = new(StringComparer.Ordinal);

        public Result<T> Get(string id) =>
            Assets.TryGetValue(id, out T? asset)
                ? Result<T>.FromSuccess(asset)
                : Result<T>.FromFailure((string?)null);
    }

    private sealed class Fixture
    {
        public readonly TestBrickRegistry Registry = new();
        public readonly TestBrickDatabase Bricks = new();
        public readonly TestAssetDatabase<Item> Items = new();
        public readonly TestAssetDatabase<Mesh> Meshes = new();
        public readonly TestAssetDatabase<Material> Materials = new();
        public readonly BrickPreviewMeshBuilder BrickMeshBuilder;
        public readonly RegionMeshBuilder RegionMeshBuilder;
        public readonly TestBundledBlueprintDatabase BundledBlueprints = new();
        public readonly VoxelMaterials VoxelMaterials;
        public readonly FakeInputService Input = new();
        public readonly InteractionState InteractionState;
        public readonly GameplayConfig GameplayConfig = new();
        public readonly PlayerData PlayerData;
        public readonly PlayerViewModelSystem System;
        public readonly MeshRendererComponentProcessor RendererProcessor = new();

        public Fixture(string? meshId = null, Mesh? mesh = null)
        {
            if (meshId != null && mesh != null)
            {
                Meshes.Assets[meshId] = mesh;
            }

            var albedo = new TextureArray(
                "albedo",
                [
                    new Texture("missing", [255, 0, 255, 255], 1, 1, mipmaps: false),
                    new Texture(TEXTURE_A, [255, 255, 255, 255], 1, 1, mipmaps: false),
                    new Texture(TEXTURE_B, [200, 200, 200, 255], 1, 1, mipmaps: false),
                ],
                mipmaps: false
            );
            var other = new TextureArray(
                "other",
                [new Texture("other", [0, 0, 0, 255], 1, 1, mipmaps: false)],
                mipmaps: false
            );
            var textureArrays = new PBRTextureArrays(albedo, other, other, other, other, other);

            var layerLookup = new TextureArrayLayerLookup(albedo);
            var previewMeshDb = new EngineMeshPreviewDatabase(Meshes);
            BrickMeshBuilder = new BrickPreviewMeshBuilder(
                Registry,
                Bricks,
                layerLookup,
                previewMeshDb
            );
            RegionMeshBuilder = new RegionMeshBuilder(Bricks, layerLookup, previewMeshDb);
            VoxelMaterials = new VoxelMaterials(new Shader("lightedArray"), textureArrays);
            var motionProcessor = new ClientPlayerMotionProcessor(
                Input,
                new FakeWindowContext(),
                new FakePhysics(),
                new EventInvoker<PlayerMovedEvent>([])
            );
            InteractionState = new InteractionState(in motionProcessor);
            PlayerData = new PlayerData(Items);
            System = new PlayerViewModelSystem(
                PlayerData,
                Materials,
                Meshes,
                Bricks,
                BrickMeshBuilder,
                VoxelMaterials,
                InteractionState,
                Input,
                GameplayConfig,
                BundledBlueprints,
                Registry,
                RegionMeshBuilder
            );
        }

        public void ProcessRetirements(DataStore store)
        {
            RendererProcessor.Process(store, 0f);
        }

        public Brick AddBrick(
            string id,
            BrickShape shape,
            string textureId = TEXTURE_A,
            BrickAlpha alpha = BrickAlpha.Opaque,
            string? meshId = null
        ) {
            var brick = new Brick(
                id,
                alpha,
                passable: false,
                meshId,
                shape,
                new BrickTextures { Default = [textureId] }
            );
            ushort dataId = Registry.Bind(id);
            brick.BindDataID(dataId);
            Bricks.Add(dataId, brick);
            return brick;
        }

        public Item AddItem(
            string id,
            PlaceableDefinition? placeable = null,
            ModelDefinition? viewModel = null,
            ToolDefinition? tool = null
        ) {
            var item = new Item(
                id,
                name: id,
                iconID: null,
                maxStack: 100,
                placeable,
                tool,
                viewModel,
                worldModel: null
            );
            Items.Assets[id] = item;
            return item;
        }
    }

    private struct CaptureRendererAction : IForEach<MeshRendererComponent>
    {
        public MeshRenderer? Renderer;

        public void Execute(float delta, DataStore store, int entity, in MeshRendererComponent component)
        {
            Renderer = component.MeshRenderer;
        }
    }

    private struct CaptureChildAction : IForEach<ViewModelComponent, ChildComponent>
    {
        public ChildComponent? Child;

        public void Execute(float delta, DataStore store, int entity, in ViewModelComponent viewModel, in ChildComponent component)
        {
            Child = component;
        }
    }

    private static Mesh SentinelMesh(Vector3 vertex)
    {
        return new Mesh([0], [vertex], [Vector4.One], [Vector3.Zero], [Vector3.UnitY]);
    }

    private static int CreatePlayer(DataStore store, params string[] slotItemIds)
    {
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new PlayerComponent());
        store.AddOrUpdate(entity, new EquipmentComponent { ActiveInventorySlot = 0 });
        var inventory = InventoryComponent.CreatePlayer();
        for (var i = 0; i < slotItemIds.Length; i++)
        {
            SlotId slotId = i < SlotGroups.DEFAULT_HOTBAR_CAPACITY
                ? new SlotId(SlotGroups.HOTBAR, i)
                : new SlotId(SlotGroups.BACKPACK, i - SlotGroups.DEFAULT_HOTBAR_CAPACITY);
            inventory.Set(in slotId, new ItemData(slotItemIds[i], 1, 100));
        }

        store.AddOrUpdate(entity, inventory);
        return entity;
    }

    private static MeshRenderer? GetRenderer(DataStore store)
    {
        var action = new CaptureRendererAction();
        store.Query<MeshRendererComponent, CaptureRendererAction>(0f, ref action);
        return action.Renderer;
    }

    private static ChildComponent? GetChild(DataStore store)
    {
        var action = new CaptureChildAction();
        store.Query<ViewModelComponent, ChildComponent, CaptureChildAction>(0f, ref action);
        return action.Child;
    }

    private static ModelDefinition CreateMiningLaserViewModel()
    {
        return new ModelDefinition
        {
            Position = new ModelDefinition.Float3(0.5f, -0.4f, 0.1f),
            Rotation = new ModelDefinition.Float3(0f, 5f, 0f),
            Scale = new ModelDefinition.Float3(1f, 1f, 1f),
        };
    }

    private static ModelDefinition CreateDrillViewModel()
    {
        return new ModelDefinition
        {
            Position = new ModelDefinition.Float3(0.5f, -0.5f, -0.3f),
            Rotation = new ModelDefinition.Float3(0f, 5f, 0f),
            Scale = new ModelDefinition.Float3(1f, 1f, 1f),
        };
    }

    private static Quaternion ExpectedOrientation(in ModelDefinition viewModel)
    {
        return Quaternion.CreateFromYawPitchRoll(
            viewModel.Rotation.Y * MathS.DEGREES_TO_RADIANS,
            viewModel.Rotation.X * MathS.DEGREES_TO_RADIANS,
            viewModel.Rotation.Z * MathS.DEGREES_TO_RADIANS
        );
    }

    private static void AssertExactRestingPose(in ChildComponent child, in ModelDefinition viewModel)
    {
        Assert.Equal(new Vector3(viewModel.Position.X, viewModel.Position.Y, viewModel.Position.Z), child.LocalPosition);
        Assert.Equal(ExpectedOrientation(in viewModel), child.LocalOrientation);
        Assert.Equal(new Vector3(viewModel.Scale.X, viewModel.Scale.Y, viewModel.Scale.Z), child.LocalScale);
    }

    private static void AssertFinitePose(in ChildComponent child)
    {
        Assert.True(float.IsFinite(child.LocalPosition.X) && float.IsFinite(child.LocalPosition.Y) && float.IsFinite(child.LocalPosition.Z));
        Assert.True(
            float.IsFinite(child.LocalOrientation.X)
            && float.IsFinite(child.LocalOrientation.Y)
            && float.IsFinite(child.LocalOrientation.Z)
            && float.IsFinite(child.LocalOrientation.W)
        );
        Assert.True(float.IsFinite(child.LocalScale.X) && float.IsFinite(child.LocalScale.Y) && float.IsFinite(child.LocalScale.Z));
    }

    [Fact]
    public void PlaceableBrick_WithoutExplicitMeshOrMaterial_RendersResolvedVoxelMesh()
    {
        var fixture = new Fixture();
        fixture.AddBrick("test:block", BrickShape.Any, TEXTURE_A);
        fixture.AddItem(
            "test:block",
            placeable: new PlaceableDefinition { Type = PlaceableType.Brick, ID = "test:block" },
            viewModel: new ModelDefinition { Scale = new ModelDefinition.Float3(0.25f, 0.25f, 0.25f) }
        );

        var store = new DataStore();
        CreatePlayer(store, "test:block");

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.System.Tick(0.016f, store);

            MeshRenderer? renderer = GetRenderer(store);
            Assert.NotNull(renderer);
            Assert.Same(fixture.VoxelMaterials.Opaque, renderer!.Materials[0]);
            Assert.False(renderer.RenderOptions.CastShadows);
            Assert.True(renderer.RenderOptions.ReceiveShadows);
            Assert.Equal(24, renderer.Mesh.Vertices.Length);
            Assert.All(renderer.Mesh.Uv, uv => Assert.Equal(1f, uv.Z));
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void PlaceableCustomBrick_RendersCustomVoxelMesh()
    {
        Vector3 sentinel = new(0.25f, 0.5f, -0.25f);
        var fixture = new Fixture();
        fixture.Meshes.Assets["thruster.obj"] = SentinelMesh(sentinel);
        fixture.AddBrick("test:thruster", BrickShape.Custom, TEXTURE_A, meshId: "thruster.obj");
        fixture.AddItem(
            "test:thruster",
            placeable: new PlaceableDefinition { Type = PlaceableType.Brick, ID = "test:thruster" },
            viewModel: new ModelDefinition { Scale = new ModelDefinition.Float3(0.25f, 0.25f, 0.25f) }
        );

        var store = new DataStore();
        CreatePlayer(store, "test:thruster");

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.System.Tick(0.016f, store);

            MeshRenderer? renderer = GetRenderer(store);
            Assert.NotNull(renderer);
            Assert.Contains(renderer!.Mesh.Vertices, v => Vector3.Distance(v, sentinel) < 1e-5f);
            Assert.False(renderer.RenderOptions.CastShadows);
            Assert.True(renderer.RenderOptions.ReceiveShadows);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void ShapeableBrick_UpdatesViewModelWhenSelectedShapeChanges()
    {
        Vector3 slopeSentinel = new(0.1f, 0.2f, 0.3f);
        var fixture = new Fixture("slope.obj", SentinelMesh(slopeSentinel));
        fixture.AddBrick("test:panel", BrickShape.Any, TEXTURE_A);
        fixture.AddItem(
            "test:panel",
            placeable: new PlaceableDefinition { Type = PlaceableType.Brick, ID = "test:panel" },
            viewModel: new ModelDefinition { Scale = new ModelDefinition.Float3(0.25f, 0.25f, 0.25f) }
        );

        var store = new DataStore();
        CreatePlayer(store, "test:panel");

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.System.Tick(0.016f, store);
            MeshRenderer firstRenderer = GetRenderer(store)!;
            Assert.Equal(24, firstRenderer.Mesh.Vertices.Length);
            fixture.ProcessRetirements(store);

            fixture.InteractionState.SelectedShape.Set(BrickShape.Slope);
            fixture.System.Tick(0.016f, store);

            MeshRenderer secondRenderer = GetRenderer(store)!;
            Assert.NotSame(firstRenderer, secondRenderer);

            //  The retired renderer waits for its replacement to bind before it releases.
            secondRenderer.IsBound = true;
            fixture.ProcessRetirements(store);

            Assert.True(firstRenderer.IsDisposed);
            Assert.True(firstRenderer.Mesh.IsDisposed);
            Assert.Contains(secondRenderer.Mesh.Vertices, v => Vector3.Distance(v, slopeSentinel) < 1e-5f);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void SwitchingBetweenTwoPlaceableBricks_RebuildsAndDisposesPreviousOwnedMesh()
    {
        var fixture = new Fixture();
        fixture.AddBrick("test:a", BrickShape.Block, TEXTURE_A);
        fixture.AddBrick("test:b", BrickShape.Block, TEXTURE_B);
        var sharedTransform = new ModelDefinition { Scale = new ModelDefinition.Float3(0.25f, 0.25f, 0.25f) };
        fixture.AddItem("test:a", new PlaceableDefinition { Type = PlaceableType.Brick, ID = "test:a" }, sharedTransform);
        fixture.AddItem("test:b", new PlaceableDefinition { Type = PlaceableType.Brick, ID = "test:b" }, sharedTransform);

        var store = new DataStore();
        int player = CreatePlayer(store, "test:a", "test:b");

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.System.Tick(0.016f, store);
            MeshRenderer firstRenderer = GetRenderer(store)!;
            Assert.All(firstRenderer.Mesh.Uv, uv => Assert.Equal(1f, uv.Z));
            fixture.ProcessRetirements(store);

            store.AddOrUpdate(player, new EquipmentComponent { ActiveInventorySlot = 1 });
            fixture.System.Tick(0.016f, store);
            MeshRenderer secondRenderer = GetRenderer(store)!;

            Assert.NotSame(firstRenderer, secondRenderer);
            secondRenderer.IsBound = true;
            fixture.ProcessRetirements(store);
            Assert.True(firstRenderer.IsDisposed);
            Assert.True(firstRenderer.Mesh.IsDisposed);
            Assert.All(secondRenderer.Mesh.Uv, uv => Assert.Equal(2f, uv.Z));
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void ExplicitMeshAndMaterial_UsesSharedAssetsAndDoesNotDisposeSharedMesh()
    {
        var fixture = new Fixture();
        Mesh sharedMesh = SentinelMesh(new Vector3(1f, 2f, 3f));
        sharedMesh.Shared = true;
        var sharedMaterial = new Material(new Shader("lighted"));
        fixture.Meshes.Assets["items/laser.obj"] = sharedMesh;
        fixture.Materials.Assets["items/laser"] = sharedMaterial;
        fixture.AddItem(
            "laser",
            placeable: null,
            viewModel: new ModelDefinition { Mesh = "items/laser.obj", Material = "items/laser" }
        );

        var store = new DataStore();
        int player = CreatePlayer(store, "laser");

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.System.Tick(0.016f, store);
            MeshRenderer renderer = GetRenderer(store)!;

            Assert.Same(sharedMesh, renderer.Mesh);
            Assert.Same(sharedMaterial, renderer.Materials[0]);
            Assert.False(renderer.RenderOptions.CastShadows);
            Assert.True(renderer.RenderOptions.ReceiveShadows);

            store.AddOrUpdate(player, new EquipmentComponent { ActiveInventorySlot = 1 });
            fixture.System.Tick(0.016f, store);

            Assert.Null(GetRenderer(store));
            fixture.ProcessRetirements(store);
            fixture.ProcessRetirements(store);
            Assert.True(renderer.IsDisposed);
            Assert.False(sharedMesh.IsDisposed);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void MissingVisualsAndNoPlaceable_FallsBackToPlaceholderCheckerboardCube()
    {
        var fixture = new Fixture();
        fixture.AddItem(
            "test:mystery",
            placeable: null,
            viewModel: new ModelDefinition { Scale = new ModelDefinition.Float3(0.25f, 0.25f, 0.25f) }
        );

        var store = new DataStore();
        CreatePlayer(store, "test:mystery");

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.System.Tick(0.016f, store);
            MeshRenderer? renderer = GetRenderer(store);

            Assert.NotNull(renderer);
            Assert.Same(fixture.VoxelMaterials.Opaque, renderer!.Materials[0]);
            Assert.False(renderer.RenderOptions.CastShadows);
            Assert.True(renderer.RenderOptions.ReceiveShadows);
            Assert.Equal(24, renderer.Mesh.Vertices.Length);
            Assert.All(renderer.Mesh.Uv, uv => Assert.Equal(0f, uv.Z));
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Theory]
    [InlineData(BrickAlpha.Opaque)]
    [InlineData(BrickAlpha.Masked)]
    [InlineData(BrickAlpha.Blend)]
    public void PlaceableBrick_PreservesAlphaModeWithNoShadowCastingAndShadowReceiving(BrickAlpha alpha)
    {
        var fixture = new Fixture();
        fixture.AddBrick("test:alpha", BrickShape.Block, TEXTURE_A, alpha);
        fixture.AddItem(
            "test:alpha",
            placeable: new PlaceableDefinition { Type = PlaceableType.Brick, ID = "test:alpha" },
            viewModel: new ModelDefinition { Scale = new ModelDefinition.Float3(0.25f, 0.25f, 0.25f) }
        );

        var store = new DataStore();
        CreatePlayer(store, "test:alpha");

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.System.Tick(0.016f, store);
            MeshRenderer renderer = GetRenderer(store)!;

            Assert.Same(fixture.VoxelMaterials.GetMaterial(alpha), renderer.Materials[0]);
            Assert.False(renderer.RenderOptions.CastShadows);
            Assert.True(renderer.RenderOptions.ReceiveShadows);
            Assert.Equal(fixture.VoxelMaterials.GetRenderOptions(alpha).AlphaMode, renderer.RenderOptions.AlphaMode);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void ShippedPlaceableItems_OmitViewModelMeshAndMaterial()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Items", "assets")));
        IFileParser[] parsers = [new TomlParser<ItemDefinitions>()];
        var parser = new VirtualFileParseService(parsers, vfs);
        var items = new ItemDatabase(NullLogger<ItemDatabase>.Instance, parser, vfs);

        Result<Item> rock = items.Get("wb:rock");
        Result<Item> thruster = items.Get("wb:thruster");
        Result<Item> laser = items.Get("laser");

        Assert.True(rock.Success);
        Assert.NotNull(rock.Value.ViewModel);
        Assert.Null(rock.Value.ViewModel!.Value.Mesh);
        Assert.Null(rock.Value.ViewModel!.Value.Material);

        Assert.True(thruster.Success);
        Assert.NotNull(thruster.Value.ViewModel);
        Assert.Null(thruster.Value.ViewModel!.Value.Mesh);
        Assert.Null(thruster.Value.ViewModel!.Value.Material);

        Assert.True(laser.Success);
        Assert.NotNull(laser.Value.ViewModel);
        Assert.Equal("items/laser.obj", laser.Value.ViewModel!.Value.Mesh);
        Assert.Equal("items/laser", laser.Value.ViewModel!.Value.Material);
    }

    [Fact]
    public void PlaceableBlueprint_WithoutExplicitMeshOrMaterial_RendersResolvedVoxelMesh()
    {
        var fixture = new Fixture();
        Brick coreBrick = fixture.AddBrick("wb:core", BrickShape.Block, TEXTURE_A);

        var voxelObj = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        voxelObj.Set(0, 0, 0, new Voxel(coreBrick.DataID, new ShapeLight(BrickShape.Block, 0), 0));
        VoxelRegionData raw = new(1, 1, 1, voxelObj.GetChunkInfos(), null, null);
        VoxelRegionData encoded = VoxelCodec.Encode(in raw, fixture.Registry);
        fixture.BundledBlueprints.Add("builtin/core", new BlueprintData(
            new WaywardBeyond.Data.Version(DataVersion.CURRENT, "Wayward Beyond", "Blueprint"),
            "Core", 0, 0, 1, 1, 1, 1, encoded.Chunks, encoded.BrickPalette, encoded.PaintPalette));

        fixture.AddItem(
            "wb:core",
            placeable: new PlaceableDefinition { Type = PlaceableType.Blueprint, ID = "builtin/core" },
            viewModel: new ModelDefinition { Scale = new ModelDefinition.Float3(0.25f, 0.25f, 0.25f) });

        var world = new World();
        DataStore store = world.DataStore;
        int player = store.Alloc();
        store.AddOrUpdate(player, new PlayerComponent());
        var inv = InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.HOTBAR, 9));
        inv.Set(new SlotId(SlotGroups.HOTBAR, 0), new ItemData("wb:core", 1, 100));
        store.AddOrUpdate(player, inv);
        store.AddOrUpdate(player, new EquipmentComponent { ActiveInventorySlot = 0 });

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.System.Tick(0.016f, store);

            MeshRenderer? renderer = null;
            store.Query<IdentifierComponent, MeshRendererComponent>(0f, (float _, DataStore _, int _, in IdentifierComponent id, in MeshRendererComponent mr) =>
            {
                if (id.Name == "PlayerViewModel")
                {
                    renderer = mr.MeshRenderer;
                }
            });

            Assert.NotNull(renderer);
            Assert.Same(fixture.VoxelMaterials.Opaque, renderer!.Materials[0]);
            //  Texture layer 1 is TEXTURE_A (layer 0 is the missing-brick checkerboard).
            Assert.NotEmpty(renderer.Mesh.Uv);
            Assert.All(renderer.Mesh.Uv, uv => Assert.Equal(1f, uv.Z));
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Theory]
    [InlineData(MouseButton.Left)]
    [InlineData(MouseButton.Right)]
    public void DrillTool_ActivePrimaryOrSecondaryUse_VibratesModeratelyAroundRestingPose(MouseButton button)
    {
        var fixture = new Fixture();
        ModelDefinition drillViewModel = CreateDrillViewModel();
        fixture.AddItem(
            "wb:drill",
            viewModel: drillViewModel,
            tool: new ToolDefinition { Type = ToolType.Drill }
        );

        var store = new DataStore();
        int player = CreatePlayer(store, "wb:drill");
        Uuid playerUuid = store.GetUuid(player);
        var restingPosition = new Vector3(drillViewModel.Position.X, drillViewModel.Position.Y, drillViewModel.Position.Z);
        Quaternion restingOrientation = ExpectedOrientation(in drillViewModel);

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.System.Tick(0.016f, store);
            ChildComponent idleChild = Assert.IsType<ChildComponent>(GetChild(store));
            AssertExactRestingPose(in idleChild, in drillViewModel);

            fixture.Input.HeldButtons.Add(button);
            fixture.System.Tick(0.016f, store);
            ChildComponent firstActive = Assert.IsType<ChildComponent>(GetChild(store));

            fixture.System.Tick(0.016f, store);
            ChildComponent secondActive = Assert.IsType<ChildComponent>(GetChild(store));

            AssertFinitePose(in firstActive);
            AssertFinitePose(in secondActive);
            Assert.Equal(playerUuid, firstActive.Parent);
            Assert.Equal(idleChild.LocalScale, firstActive.LocalScale);
            Assert.Equal(idleChild.LocalScale, secondActive.LocalScale);

            float firstOffset = Vector3.Distance(firstActive.LocalPosition, restingPosition);
            float secondOffset = Vector3.Distance(secondActive.LocalPosition, restingPosition);
            Assert.InRange(firstOffset, 0.0005f, 0.02f);
            Assert.InRange(secondOffset, 0.0005f, 0.02f);
            Assert.NotEqual(firstActive.LocalPosition, secondActive.LocalPosition);
            Assert.NotEqual(restingOrientation, firstActive.LocalOrientation);
            Assert.NotEqual(firstActive.LocalOrientation, secondActive.LocalOrientation);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void DrillTool_ReleasingButton_ImmediatelyRestoresExactRestingPose()
    {
        var fixture = new Fixture();
        ModelDefinition drillViewModel = CreateDrillViewModel();
        fixture.AddItem(
            "wb:drill",
            viewModel: drillViewModel,
            tool: new ToolDefinition { Type = ToolType.Drill }
        );

        var store = new DataStore();
        CreatePlayer(store, "wb:drill");

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.Input.HeldButtons.Add(MouseButton.Left);
            fixture.System.Tick(0.016f, store);
            ChildComponent active = Assert.IsType<ChildComponent>(GetChild(store));
            Assert.NotEqual(
                new Vector3(drillViewModel.Position.X, drillViewModel.Position.Y, drillViewModel.Position.Z),
                active.LocalPosition
            );

            fixture.Input.HeldButtons.Remove(MouseButton.Left);
            fixture.System.Tick(0.016f, store);
            ChildComponent restored = Assert.IsType<ChildComponent>(GetChild(store));
            AssertExactRestingPose(in restored, in drillViewModel);

            fixture.System.Tick(0.016f, store);
            ChildComponent stillIdle = Assert.IsType<ChildComponent>(GetChild(store));
            AssertExactRestingPose(in stillIdle, in drillViewModel);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void DrillTool_BlockingInteraction_ImmediatelyRestoresExactRestingPose()
    {
        var fixture = new Fixture();
        ModelDefinition drillViewModel = CreateDrillViewModel();
        fixture.AddItem(
            "wb:drill",
            viewModel: drillViewModel,
            tool: new ToolDefinition { Type = ToolType.Drill }
        );

        var store = new DataStore();
        CreatePlayer(store, "wb:drill");

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.Input.HeldButtons.Add(MouseButton.Left);
            fixture.System.Tick(0.016f, store);
            ChildComponent active = Assert.IsType<ChildComponent>(GetChild(store));
            Assert.NotEqual(
                new Vector3(drillViewModel.Position.X, drillViewModel.Position.Y, drillViewModel.Position.Z),
                active.LocalPosition
            );

            using (fixture.InteractionState.BlockInteraction())
            {
                fixture.System.Tick(0.016f, store);
                ChildComponent blocked = Assert.IsType<ChildComponent>(GetChild(store));
                AssertExactRestingPose(in blocked, in drillViewModel);
            }

            fixture.System.Tick(0.016f, store);
            ChildComponent resumed = Assert.IsType<ChildComponent>(GetChild(store));
            Assert.NotEqual(
                new Vector3(drillViewModel.Position.X, drillViewModel.Position.Y, drillViewModel.Position.Z),
                resumed.LocalPosition
            );
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void DrillTool_PausingGame_ImmediatelyRestoresExactRestingPose()
    {
        var fixture = new Fixture();
        ModelDefinition drillViewModel = CreateDrillViewModel();
        fixture.AddItem(
            "wb:drill",
            viewModel: drillViewModel,
            tool: new ToolDefinition { Type = ToolType.Drill }
        );

        var store = new DataStore();
        CreatePlayer(store, "wb:drill");

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.Input.HeldButtons.Add(MouseButton.Right);
            fixture.System.Tick(0.016f, store);
            ChildComponent active = Assert.IsType<ChildComponent>(GetChild(store));
            Assert.NotEqual(
                new Vector3(drillViewModel.Position.X, drillViewModel.Position.Y, drillViewModel.Position.Z),
                active.LocalPosition
            );

            WaywardBeyondGameState.GameState.Set(GameState.Paused);
            fixture.System.Tick(0.016f, store);

            Assert.Null(GetRenderer(store));
            ChildComponent paused = Assert.IsType<ChildComponent>(GetChild(store));
            AssertExactRestingPose(in paused, in drillViewModel);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void DrillTool_SwitchingToAnotherItemOrEmptySlot_RestoresExactRestingPose()
    {
        var fixture = new Fixture();
        ModelDefinition drillViewModel = CreateDrillViewModel();
        var sprayerViewModel = new ModelDefinition
        {
            Position = new ModelDefinition.Float3(0.35f, -0.45f, -0.4f),
            Rotation = new ModelDefinition.Float3(2f, -15f, 4f),
            Scale = new ModelDefinition.Float3(0.8f, 0.8f, 0.8f),
        };
        fixture.AddItem(
            "wb:drill",
            viewModel: drillViewModel,
            tool: new ToolDefinition { Type = ToolType.Drill }
        );
        fixture.AddItem(
            "wb:sprayer",
            viewModel: sprayerViewModel,
            tool: new ToolDefinition { Type = ToolType.Sprayer }
        );

        var store = new DataStore();
        int player = CreatePlayer(store, "wb:drill", "wb:sprayer");

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.Input.HeldButtons.Add(MouseButton.Left);
            fixture.System.Tick(0.016f, store);
            ChildComponent active = Assert.IsType<ChildComponent>(GetChild(store));
            Assert.NotEqual(
                new Vector3(drillViewModel.Position.X, drillViewModel.Position.Y, drillViewModel.Position.Z),
                active.LocalPosition
            );

            store.AddOrUpdate(player, new EquipmentComponent { ActiveInventorySlot = 1 });
            fixture.System.Tick(0.016f, store);
            ChildComponent switchedToSprayer = Assert.IsType<ChildComponent>(GetChild(store));
            AssertExactRestingPose(in switchedToSprayer, in sprayerViewModel);

            store.AddOrUpdate(player, new EquipmentComponent { ActiveInventorySlot = 0 });
            fixture.System.Tick(0.016f, store);
            store.AddOrUpdate(player, new EquipmentComponent { ActiveInventorySlot = 2 });
            fixture.System.Tick(0.016f, store);

            Assert.Null(GetRenderer(store));
            ChildComponent cleared = Assert.IsType<ChildComponent>(GetChild(store));
            AssertExactRestingPose(in cleared, in drillViewModel);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void NonAnimatedItemsAndIdleTools_KeepExactRestingTransformWithNoDrift()
    {
        var fixture = new Fixture();
        ModelDefinition drillViewModel = CreateDrillViewModel();
        ModelDefinition laserViewModel = CreateMiningLaserViewModel();
        var sprayerViewModel = new ModelDefinition
        {
            Position = new ModelDefinition.Float3(0.45f, -0.42f, -0.35f),
            Rotation = new ModelDefinition.Float3(0f, -5f, 0f),
            Scale = new ModelDefinition.Float3(1f, 1f, 1f),
        };
        var brickViewModel = new ModelDefinition
        {
            Position = new ModelDefinition.Float3(0.4f, -0.4f, -0.5f),
            Rotation = new ModelDefinition.Float3(0f, -60f, 0f),
            Scale = new ModelDefinition.Float3(0.25f, 0.25f, 0.25f),
        };

        fixture.AddBrick("test:block", BrickShape.Block, TEXTURE_A);
        fixture.AddItem("wb:drill", viewModel: drillViewModel, tool: new ToolDefinition { Type = ToolType.Drill });
        fixture.AddItem("laser", viewModel: laserViewModel, tool: new ToolDefinition { Type = ToolType.Mining });
        fixture.AddItem("wb:sprayer", viewModel: sprayerViewModel, tool: new ToolDefinition { Type = ToolType.Sprayer });
        fixture.AddItem(
            "test:block",
            placeable: new PlaceableDefinition { Type = PlaceableType.Brick, ID = "test:block" },
            viewModel: brickViewModel
        );

        var store = new DataStore();
        int player = CreatePlayer(store, "wb:drill", "laser", "wb:sprayer", "test:block");

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            for (var i = 0; i < 10; i++)
            {
                fixture.System.Tick(0.016f, store);
                ChildComponent idleDrill = Assert.IsType<ChildComponent>(GetChild(store));
                AssertExactRestingPose(in idleDrill, in drillViewModel);
            }

            store.AddOrUpdate(player, new EquipmentComponent { ActiveInventorySlot = 1 });
            for (var i = 0; i < 10; i++)
            {
                fixture.System.Tick(0.016f, store);
                ChildComponent idleLaser = Assert.IsType<ChildComponent>(GetChild(store));
                AssertExactRestingPose(in idleLaser, in laserViewModel);
            }

            fixture.Input.HeldButtons.Add(MouseButton.Left);
            fixture.Input.HeldButtons.Add(MouseButton.Right);

            ModelDefinition[] nonAnimatedModels = [sprayerViewModel, brickViewModel];
            for (var slot = 2; slot <= 3; slot++)
            {
                store.AddOrUpdate(player, new EquipmentComponent { ActiveInventorySlot = slot });
                for (var i = 0; i < 10; i++)
                {
                    fixture.System.Tick(0.016f, store);
                    ChildComponent child = Assert.IsType<ChildComponent>(GetChild(store));
                    AssertExactRestingPose(in child, in nonAnimatedModels[slot - 2]);
                }
            }
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void DrillTool_VibrationTime_WrapsCleanlyAndHandlesZeroOrNonFiniteDelta()
    {
        ModelDefinition drillViewModel = CreateDrillViewModel();
        var wrappedFixture = new Fixture();
        wrappedFixture.AddItem("wb:drill", viewModel: drillViewModel, tool: new ToolDefinition { Type = ToolType.Drill });
        var wrappedStore = new DataStore();
        CreatePlayer(wrappedStore, "wb:drill");

        var referenceFixture = new Fixture();
        referenceFixture.AddItem("wb:drill", viewModel: drillViewModel, tool: new ToolDefinition { Type = ToolType.Drill });
        var referenceStore = new DataStore();
        CreatePlayer(referenceStore, "wb:drill");

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            wrappedFixture.Input.HeldButtons.Add(MouseButton.Left);
            wrappedFixture.System.Tick(0f, wrappedStore);
            ChildComponent atZero = Assert.IsType<ChildComponent>(GetChild(wrappedStore));
            AssertExactRestingPose(in atZero, in drillViewModel);

            wrappedFixture.System.Tick(float.NaN, wrappedStore);
            wrappedFixture.System.Tick(float.PositiveInfinity, wrappedStore);
            wrappedFixture.System.Tick(float.NegativeInfinity, wrappedStore);
            wrappedFixture.System.Tick(-0.016f, wrappedStore);
            ChildComponent afterInvalidDeltas = Assert.IsType<ChildComponent>(GetChild(wrappedStore));
            AssertFinitePose(in afterInvalidDeltas);
            AssertExactRestingPose(in afterInvalidDeltas, in drillViewModel);

            wrappedFixture.System.Tick(0.016f, wrappedStore);
            var wrapDelta = (float)(MathS.TIME_WRAP_SECONDS + 0.032d);
            wrappedFixture.System.Tick(wrapDelta, wrappedStore);
            ChildComponent wrappedChild = Assert.IsType<ChildComponent>(GetChild(wrappedStore));
            AssertFinitePose(in wrappedChild);

            var expectedWrappedTime = (float)MathS.Repeat(0.016d + wrapDelta, MathS.TIME_WRAP_SECONDS);
            referenceFixture.Input.HeldButtons.Add(MouseButton.Left);
            referenceFixture.System.Tick(expectedWrappedTime, referenceStore);
            ChildComponent referenceChild = Assert.IsType<ChildComponent>(GetChild(referenceStore));

            Assert.Equal(referenceChild.LocalPosition.X, wrappedChild.LocalPosition.X, precision: 5);
            Assert.Equal(referenceChild.LocalPosition.Y, wrappedChild.LocalPosition.Y, precision: 5);
            Assert.Equal(referenceChild.LocalPosition.Z, wrappedChild.LocalPosition.Z, precision: 5);
            Assert.Equal(referenceChild.LocalOrientation.X, wrappedChild.LocalOrientation.X, precision: 5);
            Assert.Equal(referenceChild.LocalOrientation.Y, wrappedChild.LocalOrientation.Y, precision: 5);
            Assert.Equal(referenceChild.LocalOrientation.Z, wrappedChild.LocalOrientation.Z, precision: 5);
            Assert.Equal(referenceChild.LocalOrientation.W, wrappedChild.LocalOrientation.W, precision: 5);

            //  Invalid deltas after active vibration preserve the finite pose without drifting or NaN.
            wrappedFixture.System.Tick(0f, wrappedStore);
            wrappedFixture.System.Tick(float.NaN, wrappedStore);
            wrappedFixture.System.Tick(float.PositiveInfinity, wrappedStore);
            wrappedFixture.System.Tick(-1f, wrappedStore);
            ChildComponent afterLateInvalidDeltas = Assert.IsType<ChildComponent>(GetChild(wrappedStore));
            AssertFinitePose(in afterLateInvalidDeltas);
            Assert.Equal(wrappedChild.LocalPosition, afterLateInvalidDeltas.LocalPosition);
            Assert.Equal(wrappedChild.LocalOrientation, afterLateInvalidDeltas.LocalOrientation);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            wrappedFixture.System.Tick(0.016f, wrappedStore);
            wrappedFixture.System.Dispose();
            referenceFixture.System.Tick(0.016f, referenceStore);
            referenceFixture.System.Dispose();
        }
    }

    [Fact]
    public void MiningLaserPrimaryFire_AppliesSlightShakeLighterThanDrillAndSecondaryStaysAtRest()
    {
        ModelDefinition sharedModel = CreateMiningLaserViewModel();
        var laserFixture = new Fixture();
        laserFixture.AddItem("test:mining_laser", viewModel: sharedModel, tool: new ToolDefinition { Type = ToolType.Mining });
        var laserStore = new DataStore();
        CreatePlayer(laserStore, "test:mining_laser");

        var drillFixture = new Fixture();
        drillFixture.AddItem("wb:drill", viewModel: sharedModel, tool: new ToolDefinition { Type = ToolType.Drill });
        var drillStore = new DataStore();
        CreatePlayer(drillStore, "wb:drill");

        var restingPosition = new Vector3(sharedModel.Position.X, sharedModel.Position.Y, sharedModel.Position.Z);
        Quaternion restingOrientation = ExpectedOrientation(in sharedModel);

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            //  Idle pose matches the configured resting ViewModel transform.
            laserFixture.System.Tick(0.016f, laserStore);
            ChildComponent idleChild = Assert.IsType<ChildComponent>(GetChild(laserStore));
            AssertExactRestingPose(in idleChild, in sharedModel);

            //  Holding secondary fire alone must not shake a mining laser.
            laserFixture.Input.HeldButtons.Add(MouseButton.Right);
            laserFixture.System.Tick(0.016f, laserStore);
            ChildComponent secondaryChild = Assert.IsType<ChildComponent>(GetChild(laserStore));
            AssertExactRestingPose(in secondaryChild, in sharedModel);
            laserFixture.Input.HeldButtons.Remove(MouseButton.Right);

            //  Holding primary fire applies a slight shake noticeably lighter than the drill vibration.
            laserFixture.Input.HeldButtons.Add(MouseButton.Left);
            drillFixture.Input.HeldButtons.Add(MouseButton.Left);
            laserFixture.System.Tick(0.016f, laserStore);
            drillFixture.System.Tick(0.016f, drillStore);

            ChildComponent firstLaser = Assert.IsType<ChildComponent>(GetChild(laserStore));
            ChildComponent firstDrill = Assert.IsType<ChildComponent>(GetChild(drillStore));
            AssertFinitePose(in firstLaser);
            float laserOffset = Vector3.Distance(firstLaser.LocalPosition, restingPosition);
            float drillOffset = Vector3.Distance(firstDrill.LocalPosition, restingPosition);
            Assert.InRange(laserOffset, 0.0005f, 0.006f);
            Assert.True(laserOffset < drillOffset, $"Expected laser shake ({laserOffset}) lighter than drill vibration ({drillOffset}).");
            Assert.NotEqual(restingOrientation, firstLaser.LocalOrientation);
            Assert.Equal(idleChild.LocalScale, firstLaser.LocalScale);

            laserFixture.System.Tick(0.016f, laserStore);
            ChildComponent secondLaser = Assert.IsType<ChildComponent>(GetChild(laserStore));
            AssertFinitePose(in secondLaser);
            Assert.NotEqual(firstLaser.LocalPosition, secondLaser.LocalPosition);
            Assert.NotEqual(firstLaser.LocalOrientation, secondLaser.LocalOrientation);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            laserFixture.System.Tick(0.016f, laserStore);
            laserFixture.System.Dispose();
            drillFixture.System.Tick(0.016f, drillStore);
            drillFixture.System.Dispose();
        }
    }

    [Fact]
    public void MiningLaserShake_RestoresExactRestingPoseOnReleaseBlockPauseAndItemSwitch()
    {
        var fixture = new Fixture();
        ModelDefinition laserModel = CreateMiningLaserViewModel();
        var otherModel = new ModelDefinition
        {
            Position = new ModelDefinition.Float3(0.3f, -0.35f, -0.25f),
            Rotation = new ModelDefinition.Float3(2f, -15f, 0f),
            Scale = new ModelDefinition.Float3(0.5f, 0.5f, 0.5f),
        };
        fixture.AddItem("test:laser", viewModel: laserModel, tool: new ToolDefinition { Type = ToolType.Mining });
        fixture.AddItem("test:other", viewModel: otherModel, tool: null);

        var store = new DataStore();
        int player = CreatePlayer(store, "test:laser", "test:other");
        var laserRestPos = new Vector3(laserModel.Position.X, laserModel.Position.Y, laserModel.Position.Z);

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            //  Release primary fire restores exact resting pose.
            fixture.Input.HeldButtons.Add(MouseButton.Left);
            fixture.System.Tick(0.016f, store);
            Assert.NotEqual(laserRestPos, Assert.IsType<ChildComponent>(GetChild(store)).LocalPosition);

            fixture.Input.HeldButtons.Remove(MouseButton.Left);
            fixture.System.Tick(0.016f, store);
            ChildComponent released = Assert.IsType<ChildComponent>(GetChild(store));
            AssertExactRestingPose(in released, in laserModel);

            //  Opening a blocking UI overlay while holding primary fire restores exact resting pose.
            fixture.Input.HeldButtons.Add(MouseButton.Left);
            fixture.System.Tick(0.016f, store);
            Assert.NotEqual(laserRestPos, Assert.IsType<ChildComponent>(GetChild(store)).LocalPosition);

            using (fixture.InteractionState.BlockInteraction())
            {
                fixture.System.Tick(0.016f, store);
                ChildComponent blocked = Assert.IsType<ChildComponent>(GetChild(store));
                AssertExactRestingPose(in blocked, in laserModel);
            }

            //  Unblocking resumes shake, then pausing restores exact resting pose.
            fixture.System.Tick(0.016f, store);
            Assert.NotEqual(laserRestPos, Assert.IsType<ChildComponent>(GetChild(store)).LocalPosition);

            WaywardBeyondGameState.GameState.Set(GameState.Paused);
            fixture.System.Tick(0.016f, store);
            Assert.Null(GetRenderer(store));
            ChildComponent paused = Assert.IsType<ChildComponent>(GetChild(store));
            AssertExactRestingPose(in paused, in laserModel);

            //  Resuming restarts shake, then switching to a non-mining item restores that item's exact resting pose.
            WaywardBeyondGameState.GameState.Set(GameState.Playing);
            fixture.System.Tick(0.016f, store);
            Assert.NotEqual(laserRestPos, Assert.IsType<ChildComponent>(GetChild(store)).LocalPosition);

            store.AddOrUpdate(player, new EquipmentComponent { ActiveInventorySlot = 1 });
            fixture.System.Tick(0.016f, store);
            ChildComponent switched = Assert.IsType<ChildComponent>(GetChild(store));
            AssertExactRestingPose(in switched, in otherModel);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void MiningLaserShake_WrapsAnimationTimeCleanlyAndHandlesInvalidDeltaSafely()
    {
        ModelDefinition laserModel = CreateMiningLaserViewModel();
        var wrappedFixture = new Fixture();
        wrappedFixture.AddItem("test:laser", viewModel: laserModel, tool: new ToolDefinition { Type = ToolType.Mining });
        var wrappedStore = new DataStore();
        CreatePlayer(wrappedStore, "test:laser");

        var referenceFixture = new Fixture();
        referenceFixture.AddItem("test:laser", viewModel: laserModel, tool: new ToolDefinition { Type = ToolType.Mining });
        var referenceStore = new DataStore();
        CreatePlayer(referenceStore, "test:laser");

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            wrappedFixture.Input.HeldButtons.Add(MouseButton.Left);
            float[] invalidDeltas = [0f, -0.016f, float.NaN, float.PositiveInfinity, float.NegativeInfinity];
            foreach (float badDelta in invalidDeltas)
            {
                wrappedFixture.System.Tick(badDelta, wrappedStore);
                ChildComponent atRest = Assert.IsType<ChildComponent>(GetChild(wrappedStore));
                AssertFinitePose(in atRest);
                AssertExactRestingPose(in atRest, in laserModel);
            }

            wrappedFixture.System.Tick(0.016f, wrappedStore);
            var wrapDelta = (float)(MathS.TIME_WRAP_SECONDS + 0.032d);
            wrappedFixture.System.Tick(wrapDelta, wrappedStore);
            ChildComponent wrappedChild = Assert.IsType<ChildComponent>(GetChild(wrappedStore));
            AssertFinitePose(in wrappedChild);

            var expectedWrappedTime = (float)MathS.Repeat(0.016d + wrapDelta, MathS.TIME_WRAP_SECONDS);
            referenceFixture.Input.HeldButtons.Add(MouseButton.Left);
            referenceFixture.System.Tick(expectedWrappedTime, referenceStore);
            ChildComponent referenceChild = Assert.IsType<ChildComponent>(GetChild(referenceStore));

            Assert.Equal(referenceChild.LocalPosition.X, wrappedChild.LocalPosition.X, precision: 5);
            Assert.Equal(referenceChild.LocalPosition.Y, wrappedChild.LocalPosition.Y, precision: 5);
            Assert.Equal(referenceChild.LocalPosition.Z, wrappedChild.LocalPosition.Z, precision: 5);
            Assert.Equal(referenceChild.LocalOrientation.X, wrappedChild.LocalOrientation.X, precision: 5);
            Assert.Equal(referenceChild.LocalOrientation.Y, wrappedChild.LocalOrientation.Y, precision: 5);
            Assert.Equal(referenceChild.LocalOrientation.Z, wrappedChild.LocalOrientation.Z, precision: 5);
            Assert.Equal(referenceChild.LocalOrientation.W, wrappedChild.LocalOrientation.W, precision: 5);

            foreach (float badDelta in invalidDeltas)
            {
                wrappedFixture.System.Tick(badDelta, wrappedStore);
                ChildComponent afterBad = Assert.IsType<ChildComponent>(GetChild(wrappedStore));
                AssertFinitePose(in afterBad);
                Assert.Equal(wrappedChild.LocalPosition, afterBad.LocalPosition);
                Assert.Equal(wrappedChild.LocalOrientation, afterBad.LocalOrientation);
            }
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            wrappedFixture.System.Tick(0.016f, wrappedStore);
            wrappedFixture.System.Dispose();
            referenceFixture.System.Tick(0.016f, referenceStore);
            referenceFixture.System.Dispose();
        }
    }

    [Fact]
    public void MiningLaserShake_KeepsFirstPersonBeamMuzzleAnchoredToShakenBarrelNozzle()
    {
        var fixture = new Fixture();
        ModelDefinition laserModel = CreateMiningLaserViewModel();
        fixture.AddItem("laser", viewModel: laserModel, tool: new ToolDefinition { Type = ToolType.Mining });
        fixture.Materials.Assets["effects/laser_beam"] = new Material(new Shader("laser_beam"), [])
        {
            AlphaMode = AlphaMode.Blend,
        };
        IAssetDatabase<Swordfish.Graphics.Particles.ParticleEffect> particleDb = new TestAssetDatabase<Swordfish.Graphics.Particles.ParticleEffect>();
        IInputService inputService = fixture.Input;
        Swordfish.Physics.IPhysics physics = new FakePhysics();
        IAssetDatabase<Material> materialDb = fixture.Materials;
        using var laserSystem = new MiningLaserEffectSystem(
            in fixture.PlayerData,
            in fixture.InteractionState,
            in inputService,
            in physics,
            in materialDb,
            in particleDb
        );

        var store = new DataStore();
        var playerTransform = new TransformComponent(
            new Vector3(3f, 2f, -1f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.25f),
            Vector3.One
        );
        int player = CreatePlayer(store, "laser");
        store.AddOrUpdate(player, playerTransform);

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.Input.HeldButtons.Add(MouseButton.Left);
            fixture.System.Tick(0.016f, store);
            laserSystem.Tick(0.016f, store);

            ChildComponent shakenChild = Assert.IsType<ChildComponent>(GetChild(store));
            Vector3 expectedMuzzle = LaserBeamGeometry.ComputeFirstPersonMuzzle(in playerTransform, in shakenChild);
            Vector3 staticMuzzle = LaserBeamGeometry.ComputeFirstPersonMuzzle(in playerTransform, laserModel);
            Assert.NotEqual(staticMuzzle, expectedMuzzle);

            Vector3 actualBeamStart = default;
            var foundBeam = false;
            store.Query<MeshRendererComponent, TransformComponent>(
                0f,
                (float _, DataStore s, int e, in MeshRendererComponent _, in TransformComponent t) =>
                {
                    if (!s.Has(typeof(ViewModelComponent), e))
                    {
                        actualBeamStart = t.Position;
                        foundBeam = true;
                    }
                }
            );

            Assert.True(foundBeam);
            Assert.Equal(expectedMuzzle.X, actualBeamStart.X, precision: 4);
            Assert.Equal(expectedMuzzle.Y, actualBeamStart.Y, precision: 4);
            Assert.Equal(expectedMuzzle.Z, actualBeamStart.Z, precision: 4);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            laserSystem.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    private static Vector3 ExtractRotationDelta(in Quaternion rest, in Quaternion actual)
    {
        Quaternion sway = Quaternion.Normalize(actual * Quaternion.Conjugate(rest));
        if (sway.W < 0f)
        {
            sway = -sway;
        }

        var imag = new Vector3(sway.X, sway.Y, sway.Z);
        float sinHalf = imag.Length();
        if (sinHalf > 1e-6f)
        {
            float angle = 2f * MathF.Atan2(sinHalf, sway.W);
            return imag * (angle / sinHalf);
        }

        return imag * 2f;
    }

    [Fact]
    public void ReactiveMotion_LagsOppositePlayerLocalVelocityAndTiltsSubtly()
    {
        var fixture = new Fixture();
        var viewModel = new ModelDefinition
        {
            Position = new ModelDefinition.Float3(0.5f, -0.4f, -0.3f),
            Rotation = new ModelDefinition.Float3(0f, 5f, 0f),
            Scale = new ModelDefinition.Float3(1f, 1f, 1f),
        };
        var restPos = new Vector3(viewModel.Position.X, viewModel.Position.Y, viewModel.Position.Z);
        Quaternion restRot = ExpectedOrientation(in viewModel);
        fixture.AddItem("test:tool", placeable: null, viewModel: viewModel);

        //  Rotate the player 90 degrees left around Y so local -Z is world -X and local +X is world -Z.
        Quaternion playerOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f);

        static Vector3 MeasurePositionOffset(Fixture f, Quaternion orientation, Vector3 worldVelocity, out Vector3 rotDelta)
        {
            var store = new DataStore();
            int player = CreatePlayer(store, "test:tool");
            store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));
            store.AddOrUpdate(player, new PhysicsComponent(0, Swordfish.Physics.BodyType.Dynamic, Swordfish.Physics.CollisionDetection.Discrete, worldVelocity, Vector3.Zero));

            f.System.Tick(0.016f, store);
            for (var i = 0; i < 20; i++)
            {
                f.System.Tick(0.016f, store);
            }

            ChildComponent child = Assert.IsType<ChildComponent>(GetChild(store));
            rotDelta = ExtractRotationDelta(ExpectedOrientation(f.Items.Assets["test:tool"].ViewModel!.Value), child.LocalOrientation);
            Vector3 rest = new(0.5f, -0.4f, -0.3f);
            return child.LocalPosition - rest;
        }

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            Vector3 forwardWorld = Vector3.Transform(-Vector3.UnitZ, playerOrientation) * 5f;
            Vector3 forwardLag = MeasurePositionOffset(fixture, playerOrientation, forwardWorld, out _);
            Assert.True(forwardLag.Z > 0.005f, $"Expected +Z lag when thrusting forward, got {forwardLag}");
            Assert.InRange(MathF.Abs(forwardLag.X), 0f, 1e-4f);

            Vector3 backLag = MeasurePositionOffset(fixture, playerOrientation, -forwardWorld, out _);
            Assert.True(backLag.Z < -0.005f, $"Expected -Z lag when thrusting backward, got {backLag}");

            Vector3 rightWorld = Vector3.Transform(Vector3.UnitX, playerOrientation) * 5f;
            Vector3 rightLag = MeasurePositionOffset(fixture, playerOrientation, rightWorld, out Vector3 rightRot);
            Assert.True(rightLag.X < -0.005f, $"Expected -X lag when strafing right, got {rightLag}");
            Assert.True(rightRot.Z < -0.005f, $"Expected subtle negative roll when strafing right, got {rightRot}");

            Vector3 leftLag = MeasurePositionOffset(fixture, playerOrientation, -rightWorld, out Vector3 leftRot);
            Assert.True(leftLag.X > 0.005f, $"Expected +X lag when strafing left, got {leftLag}");
            Assert.True(leftRot.Z > 0.005f, $"Expected subtle positive roll when strafing left, got {leftRot}");

            Vector3 upLag = MeasurePositionOffset(fixture, playerOrientation, Vector3.UnitY * 5f, out Vector3 upRot);
            Assert.True(upLag.Y < -0.005f, $"Expected -Y lag when ascending, got {upLag}");
            Assert.True(upRot.X < -0.005f, $"Expected subtle negative pitch when ascending, got {upRot}");

            Vector3 downLag = MeasurePositionOffset(fixture, playerOrientation, -Vector3.UnitY * 5f, out Vector3 downRot);
            Assert.True(downLag.Y > 0.005f, $"Expected +Y lag when descending, got {downLag}");
            Assert.True(downRot.X > 0.005f, $"Expected subtle positive pitch when descending, got {downRot}");

            Assert.Equal(restPos, new Vector3(viewModel.Position.X, viewModel.Position.Y, viewModel.Position.Z));
            Assert.Equal(restRot, ExpectedOrientation(in viewModel));
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void ReactiveMotion_SwaysOnYawPitchAndRollAndSmoothlyReturnsToRest()
    {
        var fixture = new Fixture();
        var viewModel = new ModelDefinition
        {
            Position = new ModelDefinition.Float3(0.4f, -0.4f, -0.5f),
            Rotation = new ModelDefinition.Float3(0f, -60f, 0f),
            Scale = new ModelDefinition.Float3(0.25f, 0.25f, 0.25f),
        };
        var restPos = new Vector3(viewModel.Position.X, viewModel.Position.Y, viewModel.Position.Z);
        Quaternion restRot = ExpectedOrientation(in viewModel);
        fixture.AddItem("test:block", placeable: null, viewModel: viewModel);

        var store = new DataStore();
        int player = CreatePlayer(store, "test:block");
        Quaternion orientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.35f);
        store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.System.Tick(0.016f, store);
            ChildComponent initial = Assert.IsType<ChildComponent>(GetChild(store));
            AssertExactRestingPose(in initial, in viewModel);

            for (var i = 0; i < 6; i++)
            {
                orientation = Quaternion.Normalize(orientation * Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.03f));
                store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));
                fixture.System.Tick(0.016f, store);
            }

            ChildComponent afterYawRight = Assert.IsType<ChildComponent>(GetChild(store));
            Vector3 yawPosOffset = afterYawRight.LocalPosition - restPos;
            Vector3 yawRotOffset = ExtractRotationDelta(in restRot, in afterYawRight.LocalOrientation);
            Assert.True(yawPosOffset.X < -0.002f, $"Expected -X lag on right yaw, got {yawPosOffset}");
            Assert.True(yawRotOffset.Y > 0.005f, $"Expected +Y yaw lag on right yaw, got {yawRotOffset}");

            for (var i = 0; i < 120; i++)
            {
                fixture.System.Tick(0.016f, store);
            }

            ChildComponent settledAfterYaw = Assert.IsType<ChildComponent>(GetChild(store));
            AssertExactRestingPose(in settledAfterYaw, in viewModel);

            for (var i = 0; i < 6; i++)
            {
                orientation = Quaternion.Normalize(orientation * Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.03f));
                store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));
                fixture.System.Tick(0.016f, store);
            }

            ChildComponent afterPitchUp = Assert.IsType<ChildComponent>(GetChild(store));
            Vector3 pitchPosOffset = afterPitchUp.LocalPosition - restPos;
            Vector3 pitchRotOffset = ExtractRotationDelta(in restRot, in afterPitchUp.LocalOrientation);
            Assert.True(pitchPosOffset.Y < -0.002f, $"Expected -Y lag on pitch up, got {pitchPosOffset}");
            Assert.True(pitchRotOffset.X < -0.005f, $"Expected -X pitch lag on pitch up, got {pitchRotOffset}");

            for (var i = 0; i < 120; i++)
            {
                fixture.System.Tick(0.016f, store);
            }

            for (var i = 0; i < 6; i++)
            {
                orientation = Quaternion.Normalize(orientation * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.03f));
                store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));
                fixture.System.Tick(0.016f, store);
            }

            ChildComponent afterRollLeft = Assert.IsType<ChildComponent>(GetChild(store));
            Vector3 rollRotOffset = ExtractRotationDelta(in restRot, in afterRollLeft.LocalOrientation);
            Assert.True(rollRotOffset.Z < -0.005f, $"Expected -Z roll lag when rolling left, got {rollRotOffset}");

            for (var i = 0; i < 120; i++)
            {
                fixture.System.Tick(0.016f, store);
            }

            ChildComponent finalSettled = Assert.IsType<ChildComponent>(GetChild(store));
            AssertExactRestingPose(in finalSettled, in viewModel);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void ReactiveMotion_ClampsExtremeSpeedAndRapidFlicksWithinSafeBounds()
    {
        var fixture = new Fixture();
        var viewModel = new ModelDefinition
        {
            Position = new ModelDefinition.Float3(0.4f, -0.4f, -0.5f),
            Rotation = new ModelDefinition.Float3(0f, -60f, 0f),
            Scale = new ModelDefinition.Float3(0.25f, 0.25f, 0.25f),
        };
        var restPos = new Vector3(viewModel.Position.X, viewModel.Position.Y, viewModel.Position.Z);
        Quaternion restRot = ExpectedOrientation(in viewModel);
        fixture.AddItem("test:block", placeable: null, viewModel: viewModel);

        var store = new DataStore();
        int player = CreatePlayer(store, "test:block");
        Quaternion orientation = Quaternion.Identity;
        store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));
        store.AddOrUpdate(player, new PhysicsComponent(0, Swordfish.Physics.BodyType.Dynamic, Swordfish.Physics.CollisionDetection.Discrete, new Vector3(1000f, 1000f, -1000f), Vector3.Zero));

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.System.Tick(0.016f, store);

            float maxRotMagnitude = MathF.Sqrt(
                ViewModelReactiveMotion.MAX_ROTATION_OFFSET_PITCH * ViewModelReactiveMotion.MAX_ROTATION_OFFSET_PITCH
                + ViewModelReactiveMotion.MAX_ROTATION_OFFSET_YAW * ViewModelReactiveMotion.MAX_ROTATION_OFFSET_YAW
                + ViewModelReactiveMotion.MAX_ROTATION_OFFSET_ROLL * ViewModelReactiveMotion.MAX_ROTATION_OFFSET_ROLL
            );

            for (var i = 0; i < 40; i++)
            {
                orientation = Quaternion.Normalize(orientation * Quaternion.CreateFromYawPitchRoll(-1.2f, 1.1f, 0.9f));
                store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));
                fixture.System.Tick(i % 2 == 0 ? 0.016f : 1e-6f, store);

                ChildComponent child = Assert.IsType<ChildComponent>(GetChild(store));
                Vector3 posOffset = child.LocalPosition - restPos;
                Assert.True(MathF.Abs(posOffset.X) <= ViewModelReactiveMotion.MAX_POSITION_OFFSET_X + 1e-5f);
                Assert.True(MathF.Abs(posOffset.Y) <= ViewModelReactiveMotion.MAX_POSITION_OFFSET_Y + 1e-5f);
                Assert.True(MathF.Abs(posOffset.Z) <= ViewModelReactiveMotion.MAX_POSITION_OFFSET_Z + 1e-5f);

                //  Keep held item well clear of crosshair (X=0, Y=0) and camera near plane (Z=-0.1).
                Assert.True(child.LocalPosition.X > 0.3f);
                Assert.True(child.LocalPosition.Y < -0.3f);
                Assert.True(child.LocalPosition.Z < -0.4f);

                Vector3 rotOffset = ExtractRotationDelta(in restRot, in child.LocalOrientation);
                Assert.True(rotOffset.Length() <= maxRotMagnitude + 0.01f, $"Rotation offset exceeded bound: {rotOffset.Length()} > {maxRotMagnitude}");
            }
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void ReactiveMotion_IsFrameRateIndependentForConstantMotion()
    {
        static ChildComponent RunScenario(float dt, int steps)
        {
            var fixture = new Fixture();
            var viewModel = new ModelDefinition
            {
                Position = new ModelDefinition.Float3(0.5f, -0.4f, -0.3f),
                Rotation = new ModelDefinition.Float3(0f, 5f, 0f),
                Scale = new ModelDefinition.Float3(1f, 1f, 1f),
            };
            fixture.AddItem("test:tool", placeable: null, viewModel: viewModel);

            var store = new DataStore();
            int player = CreatePlayer(store, "test:tool");
            Quaternion orientation = Quaternion.Identity;
            Vector3 localVelocity = new(3f, -2f, -4f);
            store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));
            store.AddOrUpdate(player, new PhysicsComponent(0, Swordfish.Physics.BodyType.Dynamic, Swordfish.Physics.CollisionDetection.Discrete, localVelocity, Vector3.Zero));

            fixture.System.Tick(dt, store);

            const float yawRate = -1.2f;
            for (var i = 0; i < steps; i++)
            {
                orientation = Quaternion.Normalize(orientation * Quaternion.CreateFromAxisAngle(Vector3.UnitY, yawRate * dt));
                Vector3 worldVelocity = Vector3.Transform(localVelocity, orientation);
                store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));
                store.AddOrUpdate(player, new PhysicsComponent(0, Swordfish.Physics.BodyType.Dynamic, Swordfish.Physics.CollisionDetection.Discrete, worldVelocity, Vector3.Zero));
                fixture.System.Tick(dt, store);
            }

            ChildComponent result = Assert.IsType<ChildComponent>(GetChild(store));
            fixture.System.Dispose();
            return result;
        }

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            ChildComponent coarse = RunScenario(0.016f, steps: 15);
            ChildComponent fine = RunScenario(0.004f, steps: 60);

            Assert.Equal(coarse.LocalPosition.X, fine.LocalPosition.X, precision: 4);
            Assert.Equal(coarse.LocalPosition.Y, fine.LocalPosition.Y, precision: 4);
            Assert.Equal(coarse.LocalPosition.Z, fine.LocalPosition.Z, precision: 4);
            Assert.True(MathF.Abs(Quaternion.Dot(coarse.LocalOrientation, fine.LocalOrientation)) > 0.99999f);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
        }
    }

    [Fact]
    public void ReactiveMotion_HandlesZeroNegativeHugeAndNonFiniteInputsSafely()
    {
        var fixture = new Fixture();
        var viewModel = new ModelDefinition
        {
            Position = new ModelDefinition.Float3(0.5f, -0.4f, -0.3f),
            Rotation = new ModelDefinition.Float3(0f, 5f, 0f),
            Scale = new ModelDefinition.Float3(1f, 1f, 1f),
        };
        fixture.AddItem("test:tool", placeable: null, viewModel: viewModel);

        var store = new DataStore();
        int player = CreatePlayer(store, "test:tool");
        store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(player, new PhysicsComponent(0, Swordfish.Physics.BodyType.Dynamic, Swordfish.Physics.CollisionDetection.Discrete, new Vector3(4f, 0f, -4f), Vector3.Zero));

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.System.Tick(0.016f, store);
            fixture.System.Tick(0.016f, store);
            ChildComponent beforeBadDelta = Assert.IsType<ChildComponent>(GetChild(store));

            float[] invalidDeltas = [0f, -0.016f, float.NaN, float.PositiveInfinity, float.NegativeInfinity];
            foreach (float badDelta in invalidDeltas)
            {
                fixture.System.Tick(badDelta, store);
                ChildComponent after = Assert.IsType<ChildComponent>(GetChild(store));
                Assert.Equal(beforeBadDelta.LocalPosition, after.LocalPosition);
                Assert.Equal(beforeBadDelta.LocalOrientation, after.LocalOrientation);
            }

            store.AddOrUpdate(player, new PhysicsComponent(0, Swordfish.Physics.BodyType.Dynamic, Swordfish.Physics.CollisionDetection.Discrete, new Vector3(float.NaN, float.PositiveInfinity, 0f), Vector3.Zero));
            store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, new Quaternion(0f, 0f, 0f, 0f), Vector3.One));
            fixture.System.Tick(0.016f, store);

            store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, new Quaternion(float.NaN, 0f, 0f, 1f), Vector3.One));
            fixture.System.Tick(1000f, store);

            ChildComponent afterBadComponents = Assert.IsType<ChildComponent>(GetChild(store));
            AssertFinitePose(in afterBadComponents);
            AssertExactRestingPose(in afterBadComponents, in viewModel);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void ReactiveMotion_ResetsCleanlyOnPauseItemSwitchAndRejoinWithNoSpike()
    {
        var fixture = new Fixture();
        var modelA = new ModelDefinition
        {
            Position = new ModelDefinition.Float3(0.5f, -0.4f, -0.3f),
            Rotation = new ModelDefinition.Float3(0f, 5f, 0f),
            Scale = new ModelDefinition.Float3(1f, 1f, 1f),
        };
        var modelB = new ModelDefinition
        {
            Position = new ModelDefinition.Float3(0.4f, -0.45f, -0.5f),
            Rotation = new ModelDefinition.Float3(0f, -60f, 0f),
            Scale = new ModelDefinition.Float3(0.25f, 0.25f, 0.25f),
        };
        var restPosA = new Vector3(modelA.Position.X, modelA.Position.Y, modelA.Position.Z);

        fixture.AddItem("test:a", placeable: null, viewModel: modelA);
        fixture.AddItem("test:b", placeable: null, viewModel: modelB);

        var store = new DataStore();
        int player = CreatePlayer(store, "test:a", "test:b");
        Quaternion orientation = Quaternion.Identity;
        store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));
        store.AddOrUpdate(player, new PhysicsComponent(0, Swordfish.Physics.BodyType.Dynamic, Swordfish.Physics.CollisionDetection.Discrete, new Vector3(5f, 2f, -5f), Vector3.Zero));

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.System.Tick(0.016f, store);
            for (var i = 0; i < 8; i++)
            {
                orientation = Quaternion.Normalize(orientation * Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.05f));
                store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));
                fixture.System.Tick(0.016f, store);
            }

            Assert.NotEqual(restPosA, Assert.IsType<ChildComponent>(GetChild(store)).LocalPosition);

            WaywardBeyondGameState.GameState.Set(GameState.Paused);
            fixture.System.Tick(0.016f, store);
            AssertExactRestingPose(Assert.IsType<ChildComponent>(GetChild(store)), in modelA);

            orientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 2.2f);
            store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));
            store.AddOrUpdate(player, new PhysicsComponent(0, Swordfish.Physics.BodyType.Dynamic, Swordfish.Physics.CollisionDetection.Discrete, Vector3.Zero, Vector3.Zero));

            WaywardBeyondGameState.GameState.Set(GameState.Playing);
            fixture.System.Tick(0.016f, store);
            AssertExactRestingPose(Assert.IsType<ChildComponent>(GetChild(store)), in modelA);

            fixture.System.Tick(0.016f, store);
            AssertExactRestingPose(Assert.IsType<ChildComponent>(GetChild(store)), in modelA);

            for (var i = 0; i < 6; i++)
            {
                orientation = Quaternion.Normalize(orientation * Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.05f));
                store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));
                fixture.System.Tick(0.016f, store);
            }

            Assert.NotEqual(restPosA, Assert.IsType<ChildComponent>(GetChild(store)).LocalPosition);

            orientation = Quaternion.Normalize(orientation * Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.5f));
            store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));
            store.AddOrUpdate(player, new EquipmentComponent { ActiveInventorySlot = 1 });
            fixture.System.Tick(0.016f, store);
            AssertExactRestingPose(Assert.IsType<ChildComponent>(GetChild(store)), in modelB);

            fixture.System.Tick(0.016f, store);
            AssertExactRestingPose(Assert.IsType<ChildComponent>(GetChild(store)), in modelB);

            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            store.Free(player);

            int rejoinedPlayer = CreatePlayer(store, "test:a");
            store.AddOrUpdate(rejoinedPlayer, new TransformComponent(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.9f), Vector3.One));
            WaywardBeyondGameState.GameState.Set(GameState.Playing);
            fixture.System.Tick(0.016f, store);
            fixture.System.Tick(0.016f, store);
            AssertExactRestingPose(Assert.IsType<ChildComponent>(GetChild(store)), in modelA);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void ReactiveMotion_ComposesCleanlyWithActiveDrillAndMiningLaserAnimations()
    {
        var fixture = new Fixture();
        ModelDefinition drillModel = CreateDrillViewModel();
        ModelDefinition laserModel = CreateMiningLaserViewModel();
        fixture.AddItem("wb:drill", viewModel: drillModel, tool: new ToolDefinition { Type = ToolType.Drill });
        fixture.AddItem("laser", viewModel: laserModel, tool: new ToolDefinition { Type = ToolType.Mining });

        var store = new DataStore();
        int player = CreatePlayer(store, "wb:drill", "laser");
        store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(player, new PhysicsComponent(0, Swordfish.Physics.BodyType.Dynamic, Swordfish.Physics.CollisionDetection.Discrete, new Vector3(4f, 0f, -4f), Vector3.Zero));

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            //  Build up reactive motion while moving with idle drill.
            fixture.System.Tick(0.016f, store);
            for (var i = 0; i < 15; i++)
            {
                fixture.System.Tick(0.016f, store);
            }

            ChildComponent movingIdle = Assert.IsType<ChildComponent>(GetChild(store));
            var drillRestPos = new Vector3(drillModel.Position.X, drillModel.Position.Y, drillModel.Position.Z);
            Assert.True(movingIdle.LocalPosition.X < drillRestPos.X - 0.005f);

            //  Activate drill while still moving: layers vibration on top of reactive motion.
            fixture.Input.HeldButtons.Add(MouseButton.Left);
            fixture.System.Tick(0.016f, store);
            ChildComponent movingDrilling = Assert.IsType<ChildComponent>(GetChild(store));
            AssertFinitePose(in movingDrilling);
            Assert.NotEqual(movingIdle.LocalPosition, movingDrilling.LocalPosition);
            Assert.True(movingDrilling.LocalPosition.X < drillRestPos.X - 0.005f);

            //  Release fire while still moving: reactive motion remains intact without snapping to rest.
            fixture.Input.HeldButtons.Remove(MouseButton.Left);
            fixture.System.Tick(0.016f, store);
            ChildComponent movingAfterRelease = Assert.IsType<ChildComponent>(GetChild(store));
            Assert.True(movingAfterRelease.LocalPosition.X < drillRestPos.X - 0.005f);

            //  Switch to mining laser, move, fire, then stop moving and release: settles cleanly to exact rest with no drift.
            store.AddOrUpdate(player, new EquipmentComponent { ActiveInventorySlot = 1 });
            fixture.System.Tick(0.016f, store);
            fixture.Input.HeldButtons.Add(MouseButton.Left);
            for (var i = 0; i < 10; i++)
            {
                fixture.System.Tick(0.016f, store);
            }

            ChildComponent movingFiringLaser = Assert.IsType<ChildComponent>(GetChild(store));
            AssertFinitePose(in movingFiringLaser);
            var laserRestPos = new Vector3(laserModel.Position.X, laserModel.Position.Y, laserModel.Position.Z);
            Assert.True(movingFiringLaser.LocalPosition.X < laserRestPos.X - 0.005f);

            fixture.Input.HeldButtons.Remove(MouseButton.Left);
            store.AddOrUpdate(player, new PhysicsComponent(0, Swordfish.Physics.BodyType.Dynamic, Swordfish.Physics.CollisionDetection.Discrete, Vector3.Zero, Vector3.Zero));
            for (var i = 0; i < 120; i++)
            {
                fixture.System.Tick(0.016f, store);
            }

            AssertExactRestingPose(Assert.IsType<ChildComponent>(GetChild(store)), in laserModel);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void ReactiveMotion_KeepsFirstPersonLaserMuzzleAndSprayerNozzleAlignedWhileMovingAndTurning()
    {
        var fixture = new Fixture();
        ModelDefinition laserModel = CreateMiningLaserViewModel();
        var sprayerModel = new ModelDefinition
        {
            Position = new ModelDefinition.Float3(0.5f, -0.5f, -0.3f),
            Rotation = new ModelDefinition.Float3(0f, 5f, 0f),
            Scale = new ModelDefinition.Float3(1f, 1f, 1f),
        };
        fixture.AddItem("laser", viewModel: laserModel, tool: new ToolDefinition { Type = ToolType.Mining });
        fixture.AddItem("wb:sprayer", viewModel: sprayerModel, tool: new ToolDefinition { Type = ToolType.Sprayer });

        fixture.Materials.Assets["effects/laser_beam"] = new Material(new Shader("laser_beam"), [])
        {
            AlphaMode = AlphaMode.Blend,
        };
        IAssetDatabase<Material> materialDb = fixture.Materials;
        IAssetDatabase<Swordfish.Graphics.Particles.ParticleEffect> particleDb = new TestAssetDatabase<Swordfish.Graphics.Particles.ParticleEffect>();
        IInputService input = fixture.Input;
        Swordfish.Physics.IPhysics physics = new FakePhysics();

        using var laserSystem = new MiningLaserEffectSystem(
            in fixture.PlayerData,
            in fixture.InteractionState,
            in input,
            in physics,
            in materialDb,
            in particleDb
        );
        var sprayerSystem = new PaintSprayerEffectSystem(
            in fixture.PlayerData,
            in fixture.InteractionState,
            in input,
            in physics
        );

        var store = new DataStore();
        int player = CreatePlayer(store, "laser", "wb:sprayer");
        Quaternion orientation = Quaternion.Identity;
        store.AddOrUpdate(player, new TransformComponent(new Vector3(2f, 1f, -3f), orientation, Vector3.One));
        store.AddOrUpdate(player, new PhysicsComponent(0, Swordfish.Physics.BodyType.Dynamic, Swordfish.Physics.CollisionDetection.Discrete, new Vector3(4f, 2f, -4f), Vector3.Zero));

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.Input.HeldButtons.Add(MouseButton.Left);
            fixture.System.Tick(0.016f, store);
            for (var i = 0; i < 8; i++)
            {
                orientation = Quaternion.Normalize(orientation * Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.04f));
                store.AddOrUpdate(player, new TransformComponent(new Vector3(2f, 1f, -3f), orientation, Vector3.One));
                fixture.System.Tick(0.016f, store);
            }

            laserSystem.Tick(0.016f, store);

            Assert.True(store.TryGet(player, out TransformComponent laserPlayerTransform));
            ChildComponent laserChild = Assert.IsType<ChildComponent>(GetChild(store));
            Vector3 expectedLaserMuzzle = LaserBeamGeometry.ComputeFirstPersonMuzzle(in laserPlayerTransform, in laserChild);
            Vector3 staticLaserMuzzle = LaserBeamGeometry.ComputeFirstPersonMuzzle(in laserPlayerTransform, laserModel);
            Assert.NotEqual(staticLaserMuzzle, expectedLaserMuzzle);

            Vector3? actualBeamStart = null;
            store.Query<MeshRendererComponent, TransformComponent>(
                0f,
                (float _, DataStore s, int e, in MeshRendererComponent _, in TransformComponent t) =>
                {
                    if (!s.Has(typeof(ViewModelComponent), e))
                    {
                        actualBeamStart = t.Position;
                    }
                }
            );
            Assert.NotNull(actualBeamStart);
            Assert.Equal(expectedLaserMuzzle.X, actualBeamStart!.Value.X, precision: 4);
            Assert.Equal(expectedLaserMuzzle.Y, actualBeamStart.Value.Y, precision: 4);
            Assert.Equal(expectedLaserMuzzle.Z, actualBeamStart.Value.Z, precision: 4);

            store.AddOrUpdate(player, new EquipmentComponent { ActiveInventorySlot = 1 });
            fixture.System.Tick(0.016f, store);
            for (var i = 0; i < 8; i++)
            {
                orientation = Quaternion.Normalize(orientation * Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.04f));
                store.AddOrUpdate(player, new TransformComponent(new Vector3(2f, 1f, -3f), orientation, Vector3.One));
                fixture.System.Tick(0.016f, store);
            }

            laserSystem.Tick(0.016f, store);
            sprayerSystem.Tick(0.016f, store);

            Assert.True(store.TryGet(player, out TransformComponent sprayerPlayerTransform));
            ChildComponent sprayerChild = Assert.IsType<ChildComponent>(GetChild(store));
            Vector3 expectedSprayerNozzle = PaintSprayerGeometry.ComputeFirstPersonNozzle(in sprayerPlayerTransform, in sprayerChild);
            Vector3 staticSprayerNozzle = PaintSprayerGeometry.ComputeFirstPersonNozzle(in sprayerPlayerTransform, sprayerModel);
            Assert.NotEqual(staticSprayerNozzle, expectedSprayerNozzle);

            Vector3? actualNozzlePos = null;
            store.Query<Swordfish.Graphics.Particles.ParticleEmitterComponent, TransformComponent>(
                0f,
                (float _, DataStore _, int _, in Swordfish.Graphics.Particles.ParticleEmitterComponent emitter, in TransformComponent t) =>
                {
                    if (emitter.EffectId == "paint_sprayer_stream")
                    {
                        actualNozzlePos = t.Position;
                    }
                }
            );
            Assert.NotNull(actualNozzlePos);
            Assert.Equal(expectedSprayerNozzle.X, actualNozzlePos!.Value.X, precision: 4);
            Assert.Equal(expectedSprayerNozzle.Y, actualNozzlePos.Value.Y, precision: 4);
            Assert.Equal(expectedSprayerNozzle.Z, actualNozzlePos.Value.Z, precision: 4);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            laserSystem.Tick(0.016f, store);
            sprayerSystem.Tick(0.016f, store);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void ReactiveMotion_StaysSmoothUnder128HzEcsWith62HzPhysicsSteps()
    {
        const float yawRate = -2.0f;
        const float physicsDt = 0.016f;
        const float ecsDt = 0.008f;

        var continuousMotion = new ViewModelReactiveMotion();
        var steppedMotion = new ViewModelReactiveMotion();

        Quaternion continuousOrientation = Quaternion.Identity;
        Quaternion steppedOrientation = Quaternion.Identity;
        continuousMotion.SeedOrientation(in continuousOrientation);
        steppedMotion.SeedOrientation(in steppedOrientation);

        PhysicsComponent idlePhysics = default;
        float maxTickToTickRipple = 0f;
        float previousSteppedYaw = 0f;

        //  Run 0.32s total: continuous updates every 8ms, stepped updates orientation only every 16ms (like Jolt under 128 TPS ECS).
        for (var tick = 0; tick < 40; tick++)
        {
            continuousOrientation = Quaternion.Normalize(
                continuousOrientation * Quaternion.CreateFromAxisAngle(Vector3.UnitY, yawRate * ecsDt)
            );
            var continuousTransform = new TransformComponent(Vector3.Zero, continuousOrientation, Vector3.One);
            continuousMotion.Update(ecsDt, hasTransform: true, in continuousTransform, hasPhysics: false, in idlePhysics);

            if (tick % 2 == 1)
            {
                steppedOrientation = Quaternion.Normalize(
                    steppedOrientation * Quaternion.CreateFromAxisAngle(Vector3.UnitY, yawRate * physicsDt)
                );
            }

            var steppedTransform = new TransformComponent(Vector3.Zero, steppedOrientation, Vector3.One);
            steppedMotion.Update(ecsDt, hasTransform: true, in steppedTransform, hasPhysics: false, in idlePhysics);

            if (tick >= 20)
            {
                float ripple = MathF.Abs(steppedMotion.RotationOffset.Y - previousSteppedYaw);
                maxTickToTickRipple = MathF.Max(maxTickToTickRipple, ripple);
            }

            previousSteppedYaw = steppedMotion.RotationOffset.Y;
        }

        //  Stepped 62.5 Hz physics under 128 Hz ECS must track continuous turning closely without 64 Hz sawtooth collapse.
        Assert.InRange(steppedMotion.RotationOffset.Y, 0.03f, ViewModelReactiveMotion.MAX_ROTATION_OFFSET_YAW);
        Assert.True(
            MathF.Abs(steppedMotion.RotationOffset.Y - continuousMotion.RotationOffset.Y) < 0.004f,
            $"Stepped yaw {steppedMotion.RotationOffset.Y} diverged from continuous {continuousMotion.RotationOffset.Y}"
        );
        Assert.True(
            MathF.Abs(steppedMotion.PositionOffset.X - continuousMotion.PositionOffset.X) < 0.002f,
            $"Stepped pos X {steppedMotion.PositionOffset.X} diverged from continuous {continuousMotion.PositionOffset.X}"
        );
        Assert.True(maxTickToTickRipple < 0.004f, $"Unexpected tick-to-tick ripple: {maxTickToTickRipple}");
    }

    [Fact]
    public void ReactiveMotion_PreservesSwayWhenCyclingBrickShapeMidFlight()
    {
        var fixture = new Fixture("slope.obj", SentinelMesh(new Vector3(0.1f, 0.2f, 0.3f)));
        var brickViewModel = new ModelDefinition
        {
            Position = new ModelDefinition.Float3(0.4f, -0.4f, -0.5f),
            Rotation = new ModelDefinition.Float3(0f, -60f, 0f),
            Scale = new ModelDefinition.Float3(0.25f, 0.25f, 0.25f),
        };
        fixture.AddBrick("test:panel", BrickShape.Any, TEXTURE_A);
        fixture.AddItem(
            "test:panel",
            placeable: new PlaceableDefinition { Type = PlaceableType.Brick, ID = "test:panel" },
            viewModel: brickViewModel
        );

        var store = new DataStore();
        int player = CreatePlayer(store, "test:panel");
        store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(player, new PhysicsComponent(0, Swordfish.Physics.BodyType.Dynamic, Swordfish.Physics.CollisionDetection.Discrete, new Vector3(5f, 0f, 0f), Vector3.Zero));

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.System.Tick(0.016f, store);
            for (var i = 0; i < 15; i++)
            {
                fixture.System.Tick(0.016f, store);
            }

            ChildComponent beforeShapeChange = Assert.IsType<ChildComponent>(GetChild(store));
            Assert.True(beforeShapeChange.LocalPosition.X < brickViewModel.Position.X - 0.01f);

            fixture.InteractionState.SelectedShape.Set(BrickShape.Slope);
            fixture.System.Tick(0.016f, store);

            ChildComponent afterShapeChange = Assert.IsType<ChildComponent>(GetChild(store));
            Assert.True(afterShapeChange.LocalPosition.X < brickViewModel.Position.X - 0.01f);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    [Fact]
    public void ReactiveMotion_DisabledByFirstPersonSwaySetting_RestoresRestingPoseAndKeepsToolAnimations()
    {
        var fixture = new Fixture();
        ModelDefinition drillViewModel = CreateDrillViewModel();
        fixture.AddItem(
            "wb:drill",
            viewModel: drillViewModel,
            tool: new ToolDefinition { Type = ToolType.Drill }
        );

        var store = new DataStore();
        int player = CreatePlayer(store, "wb:drill");
        Quaternion orientation = Quaternion.Identity;
        store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));
        store.AddOrUpdate(
            player,
            new PhysicsComponent(
                0,
                Swordfish.Physics.BodyType.Dynamic,
                Swordfish.Physics.CollisionDetection.Discrete,
                new Vector3(6f, 0f, 0f),
                Vector3.Zero
            )
        );

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            Assert.True(fixture.GameplayConfig.FirstPersonSway.Get());

            fixture.System.Tick(0.016f, store);
            for (var i = 0; i < 15; i++)
            {
                fixture.System.Tick(0.016f, store);
            }

            ChildComponent swaying = Assert.IsType<ChildComponent>(GetChild(store));
            Assert.True(swaying.LocalPosition.X < drillViewModel.Position.X - 0.01f);

            //  Disabling the setting immediately restores exact resting pose and suppresses sway while moving and turning.
            fixture.GameplayConfig.FirstPersonSway.Set(false);
            orientation = Quaternion.Normalize(orientation * Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f));
            store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));
            fixture.System.Tick(0.016f, store);

            ChildComponent disabledPose = Assert.IsType<ChildComponent>(GetChild(store));
            AssertExactRestingPose(in disabledPose, in drillViewModel);

            //  Tool use animations stay active when reactive sway is disabled.
            fixture.Input.HeldButtons.Add(MouseButton.Left);
            fixture.System.Tick(0.016f, store);
            ChildComponent drillingWhileDisabled = Assert.IsType<ChildComponent>(GetChild(store));
            Assert.NotEqual(
                new Vector3(drillViewModel.Position.X, drillViewModel.Position.Y, drillViewModel.Position.Z),
                drillingWhileDisabled.LocalPosition
            );

            fixture.Input.HeldButtons.Remove(MouseButton.Left);
            orientation = Quaternion.Normalize(orientation * Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.2f));
            store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, orientation, Vector3.One));
            store.AddOrUpdate(
                player,
                new PhysicsComponent(
                    0,
                    Swordfish.Physics.BodyType.Dynamic,
                    Swordfish.Physics.CollisionDetection.Discrete,
                    Vector3.Zero,
                    Vector3.Zero
                )
            );
            fixture.System.Tick(0.016f, store);
            ChildComponent settledWhileDisabled = Assert.IsType<ChildComponent>(GetChild(store));
            AssertExactRestingPose(in settledWhileDisabled, in drillViewModel);

            //  Re-enabling while stationary must not inject an angular spike from rotations that occurred while disabled.
            fixture.GameplayConfig.FirstPersonSway.Set(true);
            fixture.System.Tick(0.016f, store);
            ChildComponent reenabledStationary = Assert.IsType<ChildComponent>(GetChild(store));
            AssertExactRestingPose(in reenabledStationary, in drillViewModel);

            //  Subsequent motion resumes reactive sway smoothly.
            Vector3 localRightVelocity = Vector3.Transform(new Vector3(6f, 0f, 0f), orientation);
            store.AddOrUpdate(
                player,
                new PhysicsComponent(
                    0,
                    Swordfish.Physics.BodyType.Dynamic,
                    Swordfish.Physics.CollisionDetection.Discrete,
                    localRightVelocity,
                    Vector3.Zero
                )
            );
            for (var i = 0; i < 15; i++)
            {
                fixture.System.Tick(0.016f, store);
            }

            ChildComponent resumedSway = Assert.IsType<ChildComponent>(GetChild(store));
            Assert.True(resumedSway.LocalPosition.X < drillViewModel.Position.X - 0.01f);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, store);
            fixture.System.Dispose();
        }
    }

    private sealed class TestBundledBlueprintDatabase : IBundledBlueprintDatabase
    {
        private readonly Dictionary<string, BlueprintData> _blueprints = new(StringComparer.Ordinal);
        public void Add(string id, BlueprintData data) => _blueprints[id] = data;
        public IReadOnlyList<BlueprintSummary> ListSummaries() => [];
        public Result<BlueprintData> Get(string id) =>
            _blueprints.TryGetValue(id, out BlueprintData data)
                ? Result<BlueprintData>.FromSuccess(data)
                : Result<BlueprintData>.FromFailure((string?)null);
    }
}
