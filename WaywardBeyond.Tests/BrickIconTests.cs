#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Shoal.CommandLine;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Items;
using WaywardBeyond.Meshing;
using WaywardBeyond.Voxels;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Tests for runtime brick icon framing, resolution precedence, and CLI dump options.</summary>
public class BrickIconTests
{
    private const string UNKNOWN_ICON = "items/unknown.png";
    private const string CUSTOM_ICON = "items/custom.png";
    private const string BRICK_ID = "test:panel";
    private const string OVERRIDDEN_BRICK_ID = "test:overridden";
    private const string TEXTURE_ID = "test_tex";

    [Fact]
    public void ComputeIsometricCamera_FramesUnitCubeInsideNdcWithPadding()
    {
        Vector3[] cubeVertices =
        [
            new(-0.5f, -0.5f, -0.5f),
            new( 0.5f, -0.5f, -0.5f),
            new(-0.5f,  0.5f, -0.5f),
            new( 0.5f,  0.5f, -0.5f),
            new(-0.5f, -0.5f,  0.5f),
            new( 0.5f, -0.5f,  0.5f),
            new(-0.5f,  0.5f,  0.5f),
            new( 0.5f,  0.5f,  0.5f),
        ];

        (Matrix4x4 view, Matrix4x4 projection) = BrickIconRenderer.ComputeIsometricCamera(cubeVertices, padding: 0.1f);
        Matrix4x4 viewProj = view * projection;

        float maxAbsX = 0f;
        float maxAbsY = 0f;
        foreach (Vector3 v in cubeVertices)
        {
            Vector4 clip = Vector4.Transform(new Vector4(v, 1f), viewProj);
            Vector3 ndc = new(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);
            Assert.InRange(ndc.X, -1f, 1f);
            Assert.InRange(ndc.Y, -1f, 1f);
            Assert.InRange(ndc.Z, -1f, 1f);
            maxAbsX = MathF.Max(maxAbsX, MathF.Abs(ndc.X));
            maxAbsY = MathF.Max(maxAbsY, MathF.Abs(ndc.Y));
        }

        //  With 10% padding, the tightest axis should fill 1 / 1.1 ~= 0.909 of NDC half-extent.
        float expectedPeak = 1f / 1.1f;
        Assert.InRange(MathF.Max(maxAbsX, maxAbsY), expectedPeak - 0.02f, expectedPeak + 0.02f);
    }

    [Fact]
    public void GetIconShape_MapsAnyToBlockAndPreservesSpecificShapes()
    {
        var anyBrick = CreateBrick("test:any", BrickShape.Any);
        var customBrick = CreateBrick("test:custom", BrickShape.Custom);
        var slopeBrick = CreateBrick("test:slope", BrickShape.Slope);

        Assert.Equal(BrickShape.Block, BrickIconRenderer.GetIconShape(anyBrick));
        Assert.Equal(BrickShape.Custom, BrickIconRenderer.GetIconShape(customBrick));
        Assert.Equal(BrickShape.Slope, BrickIconRenderer.GetIconShape(slopeBrick));
    }

    [Fact]
    public void ItemIconDatabase_PrefersExplicitIconThenGeneratedBrickThenUnknown()
    {
        var registry = new TestBrickRegistry();
        var brickDb = new TestBrickDatabase();

        Brick panelBrick = CreateBrick(BRICK_ID, BrickShape.Any);
        ushort panelId = registry.Bind(BRICK_ID);
        brickDb.Add(panelId, panelBrick);

        Brick overriddenBrick = CreateBrick(OVERRIDDEN_BRICK_ID, BrickShape.Block);
        ushort overriddenId = registry.Bind(OVERRIDDEN_BRICK_ID);
        brickDb.Add(overriddenId, overriddenBrick);

        var unknownTex = new Texture(UNKNOWN_ICON, new byte[4], 1, 1, mipmaps: false);
        var customTex = new Texture(CUSTOM_ICON, new byte[4], 1, 1, mipmaps: false);
        var textureDb = new TestTextureDatabase(new Dictionary<string, Texture>
        {
            [UNKNOWN_ICON] = unknownTex,
            [CUSTOM_ICON] = customTex,
        });

        var itemDb = new TestItemDatabase(new Dictionary<string, Item>
        {
            ["item:auto_brick"] = new("item:auto_brick", "n", iconID: null, 100, new PlaceableDefinition { Type = PlaceableType.Brick, ID = BRICK_ID }, null, null, null),
            ["item:override_brick"] = new("item:override_brick", "n", iconID: CUSTOM_ICON, 100, new PlaceableDefinition { Type = PlaceableType.Brick, ID = OVERRIDDEN_BRICK_ID }, null, null, null),
            ["item:no_icon_tool"] = new("item:no_icon_tool", "n", iconID: null, 1, null, null, null, null),
        });

        var offscreen = new FakeOffscreenRenderer();
        var textureArrays = CreateTextureArrays();
        var meshBuilder = new BrickPreviewMeshBuilder(
            registry,
            brickDb,
            new TextureArrayLayerLookup(textureArrays.Albedo),
            new EngineMeshPreviewDatabase(new TestMeshDatabase())
        );
        var uiShader = new Shader("ui", []);
        var iconShader = new Shader("icon", []);
        var voxelMaterials = new VoxelMaterials(iconShader, CreateTextureArrays());

        using var brickIconRenderer = new BrickIconRenderer(NullLogger<BrickIconRenderer>.Instance, meshBuilder, offscreen, voxelMaterials);
        using var iconDb = new ItemIconDatabase(
            NullLogger<ItemIconDatabase>.Instance,
            uiShader,
            textureDb,
            itemDb,
            brickDb,
            brickIconRenderer,
            CommandLineArgs.Empty
        );

        Assert.Equal(1, offscreen.BatchCallCount);
        Assert.Equal(2, offscreen.RenderedNames.Count);

        Material autoBrickMat = iconDb.Get("item:auto_brick");
        Material overrideBrickMat = iconDb.Get("item:override_brick");
        Material noIconToolMat = iconDb.Get("item:no_icon_tool");
        Material missingItemMat = iconDb.Get("item:does_not_exist");

        Assert.Equal($"brick_icon:{BRICK_ID}", autoBrickMat.Textures[0].Name);
        Assert.Same(customTex, overrideBrickMat.Textures[0]);
        Assert.Same(unknownTex, noIconToolMat.Textures[0]);
        Assert.Same(unknownTex, missingItemMat.Textures[0]);
    }

    [Fact]
    public void ItemIconDatabase_BakesIsometricIconForBlueprintPlaceableItems()
    {
        var registry = new TestBrickRegistry();
        var brickDb = new TestBrickDatabase();

        Brick coreBrick = CreateBrick("wb:core", BrickShape.Block);
        ushort coreId = registry.Bind("wb:core");
        brickDb.Add(coreId, coreBrick);

        var voxelObj = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        voxelObj.Set(0, 0, 0, new Voxel(coreId, new ShapeLight(BrickShape.Block, 0), 0));
        VoxelRegionData raw = new(1, 1, 1, voxelObj.GetChunkInfos(), null, null);
        VoxelRegionData encoded = VoxelCodec.Encode(in raw, registry);
        var coreBlueprint = new BlueprintData(
            new WaywardBeyond.Data.Version(DataVersion.CURRENT, "Wayward Beyond", "Blueprint"),
            "Core", 0, 0, 1, 1, 1, 1, encoded.Chunks, encoded.BrickPalette, encoded.PaintPalette);

        var bundledBlueprints = new TestBundledBlueprintDatabase("builtin/core", coreBlueprint);
        var unknownTex = new Texture(UNKNOWN_ICON, new byte[4], 1, 1, mipmaps: false);
        var textureDb = new TestTextureDatabase(new Dictionary<string, Texture> { [UNKNOWN_ICON] = unknownTex });
        var itemDb = new TestItemDatabase(new Dictionary<string, Item>
        {
            ["wb:core"] = new("wb:core", "Core", iconID: null, 100, new PlaceableDefinition { Type = PlaceableType.Blueprint, ID = "builtin/core" }, null, null, null),
        });

        var offscreen = new FakeOffscreenRenderer();
        var textureArrays = CreateTextureArrays();
        var layerLookup = new TextureArrayLayerLookup(textureArrays.Albedo);
        var meshPreviewDb = new EngineMeshPreviewDatabase(new TestMeshDatabase());
        var meshBuilder = new BrickPreviewMeshBuilder(registry, brickDb, layerLookup, meshPreviewDb);
        var regionMeshBuilder = new RegionMeshBuilder(brickDb, layerLookup, meshPreviewDb);
        var uiShader = new Shader("ui", []);
        var iconShader = new Shader("icon", []);
        var voxelMaterials = new VoxelMaterials(iconShader, CreateTextureArrays());

        using var brickIconRenderer = new BrickIconRenderer(NullLogger<BrickIconRenderer>.Instance, meshBuilder, offscreen, voxelMaterials);
        using var iconDb = new ItemIconDatabase(
            NullLogger<ItemIconDatabase>.Instance,
            uiShader,
            textureDb,
            itemDb,
            brickDb,
            brickIconRenderer,
            CommandLineArgs.Empty,
            bundledBlueprints,
            registry,
            regionMeshBuilder
        );

        Material coreMat = iconDb.Get("wb:core");
        Assert.Equal("blueprint_icon:builtin/core", coreMat.Textures[0].Name);
        Assert.NotSame(unknownTex, coreMat.Textures[0]);
    }

    [Fact]
    public void GetDumpDirectory_HandlesFlagOptionAndAbsentArgs()
    {
        Assert.Null(ItemIconDatabase.GetDumpDirectory(CommandLineArgs.Empty));

        var withFlag = new CommandLineArgs([ItemIconDatabase.DUMP_ICONS_CLI_KEY], []);
        Assert.Equal("debug/icons", ItemIconDatabase.GetDumpDirectory(withFlag));

        var withOption = new CommandLineArgs([], [new KeyValuePair<string, string[]>(ItemIconDatabase.DUMP_ICONS_CLI_KEY, ["out/icons"])]);
        Assert.Equal("out/icons", ItemIconDatabase.GetDumpDirectory(withOption));
    }

    private static Brick CreateBrick(string id, BrickShape shape)
    {
        return new Brick(
            id,
            BrickAlpha.Opaque,
            passable: false,
            meshID: null,
            shape,
            new BrickTextures { Default = [TEXTURE_ID] }
        );
    }

    private static PBRTextureArrays CreateTextureArrays()
    {
        Texture sample = new(TEXTURE_ID, new byte[4], 1, 1, mipmaps: false);
        TextureArray array = new("test", [sample], mipmaps: false);
        return new PBRTextureArrays(array, array, array, array, array, array);
    }

    private sealed class FakeOffscreenRenderer : IOffscreenRenderer
    {
        public int BatchCallCount { get; private set; }
        public List<string> RenderedNames { get; } = [];

        public Result<Texture> RenderToTexture(in OffscreenRenderRequest request)
        {
            return RenderBatchToTextures([request])[0];
        }

        public Result<Texture>[] RenderBatchToTextures(ReadOnlySpan<OffscreenRenderRequest> requests)
        {
            BatchCallCount++;
            var results = new Result<Texture>[requests.Length];
            for (var i = 0; i < requests.Length; i++)
            {
                OffscreenRenderRequest req = requests[i];
                RenderedNames.Add(req.Name);
                results[i] = Result<Texture>.FromSuccess(new Texture(req.Name, new byte[req.Width * req.Height * 4], req.Width, req.Height, req.Mipmaps, req.ColorSpace, req.Filter));
            }

            return results;
        }
    }

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
                if (value == id) return name;
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
                if (brick.ID == id) return Result<Brick>.FromSuccess(brick);
            }

            return Result<Brick>.FromFailure((string?)null);
        }

        public Result<Brick> Get(ushort id) =>
            _bricks.TryGetValue(id, out Brick? brick) ? Result<Brick>.FromSuccess(brick) : Result<Brick>.FromFailure((string?)null);

        public List<Brick> Get(Func<Brick, bool> predicate) => [.. _bricks.Values.Where(predicate)];

        public bool IsCuller(in Voxel voxel, BrickShape shape) => false;
    }

    private sealed class TestTextureDatabase(Dictionary<string, Texture> textures) : IAssetDatabase<Texture>
    {
        public Result<Texture> Get(string id) =>
            textures.TryGetValue(id, out Texture? tex) ? Result<Texture>.FromSuccess(tex) : Result<Texture>.FromFailure((string?)null);
    }

    private sealed class TestItemDatabase(Dictionary<string, Item> items) : IAssetDatabase<Item>
    {
        public Result<Item> Get(string id) =>
            items.TryGetValue(id, out Item? item) ? Result<Item>.FromSuccess(item) : Result<Item>.FromFailure((string?)null);
    }

    private sealed class TestMeshDatabase : IAssetDatabase<Mesh>
    {
        public Result<Mesh> Get(string id) => Result<Mesh>.FromFailure((string?)null);
    }

    private sealed class TestBundledBlueprintDatabase(string id, BlueprintData data) : IBundledBlueprintDatabase
    {
        public IReadOnlyList<BlueprintSummary> ListSummaries() =>
            [new BlueprintSummary(id, data.Name, data.SizeX, data.SizeY, data.SizeZ, data.BrickCount, 0, 0)];

        public Result<BlueprintData> GetBlueprint(string queryId) =>
            string.Equals(queryId, id, StringComparison.Ordinal)
                ? Result<BlueprintData>.FromSuccess(data)
                : Result<BlueprintData>.FromFailure("Unknown blueprint");
    }
}
