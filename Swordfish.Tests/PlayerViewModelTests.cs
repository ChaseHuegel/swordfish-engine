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
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Items;
using WaywardBeyond.Voxels;
using Xunit;
using GameState = WaywardBeyond.Client.GameState;
using WaywardBeyondGameState = WaywardBeyond.Client.WaywardBeyond;

namespace Swordfish.Tests;

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
        public readonly VoxelMaterials VoxelMaterials;
        public readonly InteractionState InteractionState = new(null!);
        public readonly PlayerData PlayerData;
        public readonly PlayerViewModelSystem System;

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
            var textureArrays = new PBRTextureArrays(albedo, other, other, other, other);

            BrickMeshBuilder = new BrickPreviewMeshBuilder(Registry, Bricks, textureArrays, Meshes);
            VoxelMaterials = new VoxelMaterials(new Shader("lightedArray"), textureArrays);
            PlayerData = new PlayerData(Items);
            System = new PlayerViewModelSystem(
                PlayerData,
                Materials,
                Meshes,
                Bricks,
                BrickMeshBuilder,
                VoxelMaterials,
                InteractionState
            );
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
                new BrickTextures { Default = [textureId] },
                tags: null
            );
            ushort dataId = Registry.Bind(id);
            brick.BindDataID(dataId);
            Bricks.Add(dataId, brick);
            return brick;
        }

        public Item AddItem(
            string id,
            PlaceableDefinition? placeable = null,
            ModelDefinition? viewModel = null
        ) {
            var item = new Item(
                id,
                name: id,
                iconID: null,
                maxStack: 100,
                placeable,
                tool: null,
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

    private static Mesh SentinelMesh(Vector3 vertex)
    {
        return new Mesh([0], [vertex], [Vector4.One], [Vector3.Zero], [Vector3.UnitY]);
    }

    private static int CreatePlayer(DataStore store, params string[] slotItemIds)
    {
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new PlayerComponent());
        store.AddOrUpdate(entity, new EquipmentComponent { ActiveInventorySlot = 0 });
        var inventory = new InventoryComponent();
        for (var i = 0; i < slotItemIds.Length; i++)
        {
            inventory.Contents[i] = new ItemData(slotItemIds[i], 1, 100);
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

            fixture.InteractionState.SelectedShape.Set(BrickShape.Slope);
            fixture.System.Tick(0.016f, store);

            MeshRenderer secondRenderer = GetRenderer(store)!;
            Assert.NotSame(firstRenderer, secondRenderer);
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

            store.AddOrUpdate(player, new EquipmentComponent { ActiveInventorySlot = 1 });
            fixture.System.Tick(0.016f, store);
            MeshRenderer secondRenderer = GetRenderer(store)!;

            Assert.NotSame(firstRenderer, secondRenderer);
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
}
