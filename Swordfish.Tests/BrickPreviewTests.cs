#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;
using Xunit;
using GameState = WaywardBeyond.Client.GameState;
using WaywardBeyondGameState = WaywardBeyond.Client.WaywardBeyond;

namespace Swordfish.Tests;

/// <summary>Tests for the textured brick placement preview.</summary>
[Collection("ClientGameState")]
public class BrickPreviewTests
{
    private const string BRICK_ID = "test:block";
    private const string TEXTURE_ID = "test:texture";

    private sealed class TestBrickRegistry : IBrickRegistry
    {
        private readonly Dictionary<string, ushort> _ids = new(StringComparer.Ordinal);

        public ushort Bind(string id)
        {
            ushort value = (ushort)(_ids.Count + 1);
            _ids[id] = value;
            return value;
        }

        public ushort Id(string name)
        {
            return _ids.TryGetValue(name, out ushort id) ? id : (ushort)0;
        }

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

        public void Add(ushort id, Brick brick)
        {
            _bricks[id] = brick;
        }

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

        public Result<Brick> Get(ushort id)
        {
            return _bricks.TryGetValue(id, out Brick? brick)
                ? Result<Brick>.FromSuccess(brick)
                : Result<Brick>.FromFailure((string?)null);
        }

        public List<Brick> Get(Func<Brick, bool> predicate)
        {
            return [.. _bricks.Values.Where(predicate)];
        }

        public bool IsCuller(in Voxel voxel, BrickShape shape)
        {
            return false;
        }
    }

    private sealed class TestMeshDatabase : IAssetDatabase<Mesh>
    {
        public readonly Dictionary<string, Mesh> Meshes = [];

        public Result<Mesh> Get(string id)
        {
            return Meshes.TryGetValue(id, out Mesh? mesh)
                ? Result<Mesh>.FromSuccess(mesh)
                : Result<Mesh>.FromFailure((string?)null);
        }
    }

    private sealed class Fixture
    {
        public readonly TestBrickRegistry Registry = new();
        public readonly TestBrickDatabase Bricks = new();
        public readonly TestMeshDatabase Meshes = new();
        public readonly BrickPreviewMeshBuilder Builder;
        public readonly VoxelMaterials Materials;

        public Fixture(string? meshID = null, Mesh? mesh = null)
        {
            if (meshID != null && mesh != null)
            {
                Meshes.Meshes[meshID] = mesh;
            }

            var albedo = new TextureArray(
                "albedo",
                [
                    new Texture("missing", [255, 0, 255, 255], 1, 1, mipmaps: false),
                    new Texture(TEXTURE_ID, [255, 255, 255, 255], 1, 1, mipmaps: false),
                ],
                mipmaps: false
            );
            var other = new TextureArray(
                "other",
                [new Texture("other", [0, 0, 0, 255], 1, 1, mipmaps: false)],
                mipmaps: false
            );
            var textureArrays = new PBRTextureArrays(albedo, other, other, other, other);

            Builder = new BrickPreviewMeshBuilder(Registry, Bricks, textureArrays, Meshes);
            Materials = new VoxelMaterials(new Shader("test"), textureArrays);
        }

        public Brick AddBrick(
            string id,
            BrickShape shape,
            BrickAlpha alpha = BrickAlpha.Opaque,
            string? meshID = null,
            string[]? tags = null
        ) {
            var brick = new Brick(
                id,
                alpha,
                passable: false,
                meshID,
                shape,
                new BrickTextures { Default = [TEXTURE_ID] },
                tags
            );
            ushort dataID = Registry.Bind(id);
            brick.BindDataID(dataID);
            Bricks.Add(dataID, brick);
            return brick;
        }
    }

    private struct CountRenderersAction : IForEach<MeshRendererComponent>
    {
        public int Count;

        public void Execute(float delta, DataStore store, int entity, in MeshRendererComponent component)
        {
            Count++;
        }
    }

    private struct CaptureTransformAction : IForEach<TransformComponent, MeshRendererComponent>
    {
        public bool Found;
        public TransformComponent Transform;

        public void Execute(
            float delta,
            DataStore store,
            int entity,
            in TransformComponent transform,
            in MeshRendererComponent component
        ) {
            Found = true;
            Transform = transform;
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

    private struct CountTaggedAction : IForEach<IdentifierComponent>
    {
        public string Tag;
        public int Count;

        public void Execute(float delta, DataStore store, int entity, in IdentifierComponent identifier)
        {
            if (identifier.Tag == Tag)
            {
                Count++;
            }
        }
    }

    private static Mesh SentinelsMesh(Vector3 vertex)
    {
        return new Mesh([0], [vertex], [Vector4.One], [Vector3.Zero], [Vector3.UnitY]);
    }

    private static void AssertPreviewColors(IReadOnlyList<Vector4> colors)
    {
        Assert.All(colors, color =>
        {
            Assert.Equal(1f, color.X, 3);
            Assert.Equal(1f, color.Y, 3);
            Assert.Equal(1f, color.Z, 3);
        });
    }

    [Fact]
    public void Build_Block_ProducesCenteredTexturedCube()
    {
        var fixture = new Fixture();
        Brick brick = fixture.AddBrick(BRICK_ID, BrickShape.Any);

        Result<Mesh> result = fixture.Builder.Build(brick, BrickShape.Block, Orientation.Identity);

        Assert.True(result.Success);
        Mesh mesh = result.Value;
        Assert.Equal(24, mesh.Vertices.Length);
        Assert.Equal(36, mesh.Triangles.Length);
        Assert.Equal(0f, mesh.Bounds.Center.X, 4);
        Assert.Equal(0f, mesh.Bounds.Center.Y, 4);
        Assert.Equal(0f, mesh.Bounds.Center.Z, 4);
        Assert.Equal(0.5f, mesh.Bounds.Extents.X, 4);
        Assert.All(mesh.Uv, uv => Assert.Equal(1f, uv.Z));
        AssertPreviewColors(mesh.Colors);
    }

    [Theory]
    [InlineData(BrickShape.Slab, "slab.obj")]
    [InlineData(BrickShape.Stair, "stair.obj")]
    [InlineData(BrickShape.Slope, "slope.obj")]
    [InlineData(BrickShape.Column, "column.obj")]
    [InlineData(BrickShape.Plate, "plate.obj")]
    public void Build_ShapeMeshes_UseShapeGeometry(BrickShape shape, string meshID)
    {
        Vector3 sentinel = new(0.25f, 0.5f, -0.25f);
        var fixture = new Fixture(meshID, SentinelsMesh(sentinel));
        Brick brick = fixture.AddBrick(BRICK_ID, BrickShape.Any);

        Result<Mesh> result = fixture.Builder.Build(brick, shape, Orientation.Identity);

        Assert.True(result.Success);
        Assert.Contains(result.Value.Vertices, vertex => Vector3.Distance(vertex, sentinel) < 1e-5f);
        AssertPreviewColors(result.Value.Colors);
    }

    [Fact]
    public void Build_CustomShape_UsesTheBrickMesh()
    {
        Vector3 sentinel = new(0.25f, 0.5f, -0.25f);
        var fixture = new Fixture("custom.obj", SentinelsMesh(sentinel));
        Brick brick = fixture.AddBrick(BRICK_ID, BrickShape.Custom, meshID: "custom.obj");

        Result<Mesh> result = fixture.Builder.Build(brick, BrickShape.Custom, Orientation.Identity);

        Assert.True(result.Success);
        Assert.Contains(result.Value.Vertices, vertex => Vector3.Distance(vertex, sentinel) < 1e-5f);
    }

    [Fact]
    public void Build_UnregisteredBrick_ReturnsFailure()
    {
        var fixture = new Fixture();
        var brick = new Brick(
            "test:missing",
            BrickAlpha.Opaque,
            passable: false,
            meshID: null,
            BrickShape.Any,
            new BrickTextures { Default = [TEXTURE_ID] },
            tags: null
        );

        Result<Mesh> result = fixture.Builder.Build(brick, BrickShape.Block, Orientation.Identity);

        Assert.False(result.Success);
    }

    [Fact]
    public void Build_NonOrientableBlock_IgnoresTheRequestedOrientation()
    {
        var fixture = new Fixture();
        Brick brick = fixture.AddBrick(BRICK_ID, BrickShape.Block);
        var rotated = new Orientation(pitch: 1, yaw: 0, roll: 0);

        Result<Mesh> identity = fixture.Builder.Build(brick, BrickShape.Block, Orientation.Identity);
        Result<Mesh> turned = fixture.Builder.Build(brick, BrickShape.Block, rotated);

        Assert.True(identity.Success);
        Assert.True(turned.Success);
        Assert.Equal(identity.Value.Vertices, turned.Value.Vertices);
    }

    [Fact]
    public void Build_OrientableShape_BakesTheRequestedOrientation()
    {
        var fixture = new Fixture("slope.obj", SentinelsMesh(Vector3.UnitY));
        Brick brick = fixture.AddBrick(BRICK_ID, BrickShape.Any);
        var rotated = new Orientation(pitch: 1, yaw: 0, roll: 0);

        Result<Mesh> result = fixture.Builder.Build(brick, BrickShape.Slope, rotated);

        Assert.True(result.Success);
        Assert.Contains(result.Value.Vertices, vertex => Vector3.Distance(vertex, Vector3.UnitZ) < 1e-5f);
        Assert.DoesNotContain(result.Value.Vertices, vertex => Vector3.Distance(vertex, Vector3.UnitY) < 1e-5f);
    }

    [Fact]
    public void Materials_SelectTheBrickAlphaMode()
    {
        var fixture = new Fixture();

        Assert.True(fixture.Materials.GetMaterial(BrickAlpha.Blend).Transparent);
        Assert.False(fixture.Materials.GetMaterial(BrickAlpha.Masked).Transparent);
        Assert.Equal(AlphaMode.Masked, fixture.Materials.GetRenderOptions(BrickAlpha.Masked).AlphaMode);
        Assert.Equal(AlphaMode.Blend, fixture.Materials.GetRenderOptions(BrickAlpha.Blend).AlphaMode);
    }

    [Theory]
    [InlineData(BrickAlpha.Opaque)]
    [InlineData(BrickAlpha.Masked)]
    [InlineData(BrickAlpha.Blend)]
    public void Build_EveryAlphaMode_ProducesGeometry(BrickAlpha alpha)
    {
        var fixture = new Fixture();
        Brick brick = fixture.AddBrick(BRICK_ID, BrickShape.Any, alpha);

        Result<Mesh> result = fixture.Builder.Build(brick, BrickShape.Block, Orientation.Identity);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Value.Vertices);
        Assert.NotEmpty(result.Value.Triangles);
    }

    [Fact]
    public void System_ShowsAndHidesThePreviewEntity()
    {
        var fixture = new Fixture();
        Brick brick = fixture.AddBrick(BRICK_ID, BrickShape.Any);
        var state = new BrickPreviewState();
        var system = new BrickPreviewSystem(state, fixture.Builder, fixture.Materials);
        var store = new DataStore();

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            state.Show(brick, BrickShape.Block, Orientation.Identity, new Vector3(1, 2, 3), Quaternion.Identity);
            system.Tick(0.016f, store);
            Assert.Equal(1, CountRenderers(store));
            MeshRenderer firstRenderer = GetRenderer(store)!;

            system.Tick(0.016f, store);
            Assert.Same(firstRenderer, GetRenderer(store));

            state.Hide();
            system.Tick(0.016f, store);
            Assert.Equal(0, CountRenderers(store));
            Assert.Equal(1, CountTagged(store, "game"));
            Assert.False(firstRenderer.IsDisposed);

            var cleanupSystem = new CleanupMeshRendererSystem();
            cleanupSystem.Tick(0f, store);
            Assert.True(firstRenderer.IsDisposed);
            Assert.True(firstRenderer.Mesh.IsDisposed);

            state.Show(brick, BrickShape.Block, Orientation.Identity, new Vector3(4, 5, 6), Quaternion.Identity);
            system.Tick(0.016f, store);
            Assert.Equal(1, CountRenderers(store));
            Assert.NotSame(firstRenderer, GetRenderer(store));
            Assert.True(TryGetPreviewTransform(store, out TransformComponent transform));
            Assert.Equal(new Vector3(4, 5, 6), transform.Position);
            Assert.Equal(Quaternion.Identity, transform.Orientation);
            Assert.InRange(transform.Scale.X, 0.98f, 1.0f);

            WaywardBeyondGameState.GameState.Set(GameState.Paused);
            system.Tick(0.016f, store);
            Assert.Equal(1, CountRenderers(store));

            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            system.Tick(0.016f, store);
            Assert.False(state.Visible);
            Assert.Null(state.Brick);
            Assert.Equal(0, CountRenderers(store));
            Assert.Equal(0, CountTagged(store, "game"));
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            system.Dispose();
        }
    }

    [Fact]
    public void System_ReplacedRendererWaitsForTheNewRenderer()
    {
        var fixture = new Fixture("slab.obj", SentinelsMesh(Vector3.UnitY));
        Brick brick = fixture.AddBrick(BRICK_ID, BrickShape.Any);
        var state = new BrickPreviewState();
        var system = new BrickPreviewSystem(state, fixture.Builder, fixture.Materials);
        var store = new DataStore();

        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            state.Show(brick, BrickShape.Block, Orientation.Identity, Vector3.Zero, Quaternion.Identity);
            system.Tick(0.016f, store);
            MeshRenderer firstRenderer = GetRenderer(store)!;

            state.Show(brick, BrickShape.Slab, Orientation.Identity, Vector3.Zero, Quaternion.Identity);
            system.Tick(0.016f, store);
            MeshRenderer secondRenderer = GetRenderer(store)!;

            Assert.NotSame(firstRenderer, secondRenderer);
            Assert.False(firstRenderer.IsDisposed);

            var cleanupSystem = new CleanupMeshRendererSystem();
            cleanupSystem.Tick(0f, store);
            Assert.False(firstRenderer.IsDisposed);

            secondRenderer.IsBound = true;
            cleanupSystem.Tick(0f, store);
            Assert.True(firstRenderer.IsDisposed);
            Assert.True(firstRenderer.Mesh.IsDisposed);
            Assert.False(secondRenderer.IsDisposed);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            system.Tick(0.016f, store);
            system.Dispose();
        }
    }

    private static int CountRenderers(DataStore store)
    {
        var action = new CountRenderersAction();
        store.Query<MeshRendererComponent, CountRenderersAction>(0f, ref action);
        return action.Count;
    }

    private static MeshRenderer? GetRenderer(DataStore store)
    {
        var action = new CaptureRendererAction();
        store.Query<MeshRendererComponent, CaptureRendererAction>(0f, ref action);
        return action.Renderer;
    }

    private static int CountTagged(DataStore store, string tag)
    {
        var action = new CountTaggedAction { Tag = tag };
        store.Query<IdentifierComponent, CountTaggedAction>(0f, ref action);
        return action.Count;
    }

    private static bool TryGetPreviewTransform(DataStore store, out TransformComponent transform)
    {
        var action = new CaptureTransformAction();
        store.Query<TransformComponent, MeshRendererComponent, CaptureTransformAction>(0f, ref action);
        transform = action.Transform;
        return action.Found;
    }
}
