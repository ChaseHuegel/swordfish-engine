#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Voxels;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Tests per-face texture mapping on non-block brick shapes.</summary>
public class ShapedBrickTextureTests
{
    private const string DEFAULT_TEX = "tex_default";
    private const string TOP_TEX = "tex_top";
    private const string BOTTOM_TEX = "tex_bottom";
    private const string FRONT_TEX = "tex_front";
    private const string BACK_TEX = "tex_back";
    private const string LEFT_TEX = "tex_left";
    private const string RIGHT_TEX = "tex_right";
    private const string VARIANT_A = "tex_var_a";
    private const string VARIANT_B = "tex_var_b";

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

    private sealed class TestMeshDatabase : IAssetDatabase<Mesh>
    {
        public readonly Dictionary<string, Mesh> Meshes = [];

        public Result<Mesh> Get(string id) =>
            Meshes.TryGetValue(id, out Mesh? mesh)
                ? Result<Mesh>.FromSuccess(mesh)
                : Result<Mesh>.FromFailure((string?)null);
    }

    private sealed class Fixture
    {
        public readonly TestBrickRegistry Registry = new();
        public readonly TestBrickDatabase Bricks = new();
        public readonly TestMeshDatabase Meshes = new();
        public readonly TextureArray Albedo;
        public readonly BrickPreviewMeshBuilder Builder;

        public Fixture(string meshID, Mesh mesh)
        {
            Meshes.Meshes[meshID] = mesh;

            Albedo = new TextureArray(
                "albedo",
                [
                    CreateTexture("missing"),
                    CreateTexture(DEFAULT_TEX),
                    CreateTexture(TOP_TEX),
                    CreateTexture(BOTTOM_TEX),
                    CreateTexture(FRONT_TEX),
                    CreateTexture(BACK_TEX),
                    CreateTexture(LEFT_TEX),
                    CreateTexture(RIGHT_TEX),
                    CreateTexture(VARIANT_A),
                    CreateTexture(VARIANT_B),
                ],
                mipmaps: false
            );
            var other = new TextureArray("other", [CreateTexture("other")], mipmaps: false);
            var textureArrays = new PBRTextureArrays(Albedo, other, other, other, other);

            Builder = new BrickPreviewMeshBuilder(Registry, Bricks, textureArrays, Meshes);
        }

        public Brick AddBrick(BrickShape shape, BrickTextures textures, string? meshID = null)
        {
            const string brickID = "test:shaped";
            var brick = new Brick(
                brickID,
                BrickAlpha.Opaque,
                passable: false,
                meshID,
                shape,
                textures
            );
            ushort dataID = Registry.Bind(brickID);
            brick.BindDataID(dataID);
            Bricks.Add(dataID, brick);
            return brick;
        }

        public int IndexOf(string name) => Albedo.IndexOf(name);

        private static Texture CreateTexture(string name) =>
            new(name, [255, 255, 255, 255], 1, 1, mipmaps: false);
    }

    private static Mesh MeshFromNormals(params Vector3[] normals)
    {
        var triangles = new uint[normals.Length];
        var vertices = new Vector3[normals.Length];
        var colors = new Vector4[normals.Length];
        var uvs = new Vector3[normals.Length];

        for (var i = 0; i < normals.Length; i++)
        {
            triangles[i] = (uint)i;
            vertices[i] = new Vector3(i * 0.1f, 0f, 0f);
            colors[i] = Vector4.One;
            uvs[i] = new Vector3(0.5f, 0.5f, 0f);
        }

        return new Mesh(triangles, vertices, colors, uvs, normals);
    }

    private static Mesh CreateSlopeLikeMesh() =>
        MeshFromNormals(
            new Vector3( 1f,  0f,  0f),           // 0: Right
            new Vector3(-1f,  0f,  0f),           // 1: Left
            new Vector3( 0f, -1f,  0f),           // 2: Bottom
            new Vector3( 0f,  0f, -1f),           // 3: Back
            new Vector3( 0f,  0.7071f,  0.7071f)  // 4: Angled slope face
        );

    private static Mesh CreateSixFaceMesh() =>
        MeshFromNormals(
            new Vector3( 0f,  1f,  0f), // 0: Top
            new Vector3( 0f, -1f,  0f), // 1: Bottom
            new Vector3( 0f,  0f,  1f), // 2: Front
            new Vector3( 0f,  0f, -1f), // 3: Back
            new Vector3(-1f,  0f,  0f), // 4: Left
            new Vector3( 1f,  0f,  0f)  // 5: Right
        );

    [Fact]
    public void Slope_MapsPerFaceTexturesAndUsesFrontForAngledFace()
    {
        var fixture = new Fixture("slope.obj", CreateSlopeLikeMesh());
        Brick brick = fixture.AddBrick(
            BrickShape.Any,
            new BrickTextures
            {
                Default = [DEFAULT_TEX],
                Top = [TOP_TEX],
                Bottom = [BOTTOM_TEX],
                Front = [FRONT_TEX],
                Back = [BACK_TEX],
                Left = [LEFT_TEX],
                Right = [RIGHT_TEX],
            }
        );

        Result<Mesh> result = fixture.Builder.Build(brick, BrickShape.Slope, Orientation.Identity);

        Assert.True(result.Success);
        Vector3[] uvs = result.Value.Uv;
        Assert.Equal(fixture.IndexOf(RIGHT_TEX), uvs[0].Z);
        Assert.Equal(fixture.IndexOf(LEFT_TEX), uvs[1].Z);
        Assert.Equal(fixture.IndexOf(BOTTOM_TEX), uvs[2].Z);
        Assert.Equal(fixture.IndexOf(BACK_TEX), uvs[3].Z);
        Assert.Equal(fixture.IndexOf(FRONT_TEX), uvs[4].Z);
    }

    [Fact]
    public void Slope_AngledFaceFallsBackToTopWhenFrontIsUnset()
    {
        var fixture = new Fixture("slope.obj", CreateSlopeLikeMesh());
        Brick brick = fixture.AddBrick(
            BrickShape.Any,
            new BrickTextures
            {
                Default = [DEFAULT_TEX],
                Top = [TOP_TEX],
                Left = [LEFT_TEX],
                Right = [RIGHT_TEX],
            }
        );

        Result<Mesh> result = fixture.Builder.Build(brick, BrickShape.Slope, Orientation.Identity);

        Assert.True(result.Success);
        Vector3[] uvs = result.Value.Uv;
        Assert.Equal(fixture.IndexOf(RIGHT_TEX), uvs[0].Z);
        Assert.Equal(fixture.IndexOf(LEFT_TEX), uvs[1].Z);
        Assert.Equal(fixture.IndexOf(DEFAULT_TEX), uvs[2].Z);
        Assert.Equal(fixture.IndexOf(DEFAULT_TEX), uvs[3].Z);
        Assert.Equal(fixture.IndexOf(TOP_TEX), uvs[4].Z);
    }

    [Fact]
    public void Slope_WithoutDefaultTextureUsesPerFaceTexturesWithoutMissingFallback()
    {
        var fixture = new Fixture("slope.obj", CreateSlopeLikeMesh());
        Brick brick = fixture.AddBrick(
            BrickShape.Any,
            new BrickTextures
            {
                Top = [TOP_TEX],
                Bottom = [BOTTOM_TEX],
                Front = [FRONT_TEX],
                Back = [BACK_TEX],
                Left = [LEFT_TEX],
                Right = [RIGHT_TEX],
            }
        );

        Result<Mesh> result = fixture.Builder.Build(brick, BrickShape.Slope, Orientation.Identity);

        Assert.True(result.Success);
        Vector3[] uvs = result.Value.Uv;
        Assert.Equal(fixture.IndexOf(RIGHT_TEX), uvs[0].Z);
        Assert.Equal(fixture.IndexOf(LEFT_TEX), uvs[1].Z);
        Assert.Equal(fixture.IndexOf(BOTTOM_TEX), uvs[2].Z);
        Assert.Equal(fixture.IndexOf(BACK_TEX), uvs[3].Z);
        Assert.Equal(fixture.IndexOf(FRONT_TEX), uvs[4].Z);
    }

    [Theory]
    [InlineData(BrickShape.Slab, "slab.obj")]
    [InlineData(BrickShape.Stair, "stair.obj")]
    [InlineData(BrickShape.Column, "column.obj")]
    [InlineData(BrickShape.Plate, "plate.obj")]
    public void CardinalShapes_MapAllSixFaces(BrickShape shape, string meshID)
    {
        var fixture = new Fixture(meshID, CreateSixFaceMesh());
        Brick brick = fixture.AddBrick(
            BrickShape.Any,
            new BrickTextures
            {
                Default = [DEFAULT_TEX],
                Top = [TOP_TEX],
                Bottom = [BOTTOM_TEX],
                Front = [FRONT_TEX],
                Back = [BACK_TEX],
                Left = [LEFT_TEX],
                Right = [RIGHT_TEX],
            }
        );

        Result<Mesh> result = fixture.Builder.Build(brick, shape, Orientation.Identity);

        Assert.True(result.Success);
        Vector3[] uvs = result.Value.Uv;
        Assert.Equal(fixture.IndexOf(TOP_TEX), uvs[0].Z);
        Assert.Equal(fixture.IndexOf(BOTTOM_TEX), uvs[1].Z);
        Assert.Equal(fixture.IndexOf(FRONT_TEX), uvs[2].Z);
        Assert.Equal(fixture.IndexOf(BACK_TEX), uvs[3].Z);
        Assert.Equal(fixture.IndexOf(LEFT_TEX), uvs[4].Z);
        Assert.Equal(fixture.IndexOf(RIGHT_TEX), uvs[5].Z);
    }

    [Fact]
    public void RotatedSlope_PreservesLocalFaceTextures()
    {
        var fixture = new Fixture("slope.obj", CreateSlopeLikeMesh());
        Brick brick = fixture.AddBrick(
            BrickShape.Any,
            new BrickTextures
            {
                Default = [DEFAULT_TEX],
                Top = [TOP_TEX],
                Bottom = [BOTTOM_TEX],
                Front = [FRONT_TEX],
                Back = [BACK_TEX],
                Left = [LEFT_TEX],
                Right = [RIGHT_TEX],
            }
        );

        var rotated = new Orientation(pitch: 1, yaw: 2, roll: 0);
        Result<Mesh> result = fixture.Builder.Build(brick, BrickShape.Slope, rotated);

        Assert.True(result.Success);
        Vector3[] uvs = result.Value.Uv;
        Assert.Equal(fixture.IndexOf(RIGHT_TEX), uvs[0].Z);
        Assert.Equal(fixture.IndexOf(LEFT_TEX), uvs[1].Z);
        Assert.Equal(fixture.IndexOf(BOTTOM_TEX), uvs[2].Z);
        Assert.Equal(fixture.IndexOf(BACK_TEX), uvs[3].Z);
        Assert.Equal(fixture.IndexOf(FRONT_TEX), uvs[4].Z);
    }

    [Fact]
    public void CustomMesh_RespectsDominantAxisSecondaryThresholdAndZeroNormalFallback()
    {
        Mesh customMesh = MeshFromNormals(
            new Vector3(0.9701f, 0f, -0.2425f), // 0: Mostly Right, minor Back tilt -> Default when Right unset
            new Vector3(0f, 0.8f, 0.6f),        // 1: Steep Top+Front -> Top, or Front fallback
            Vector3.Zero                         // 2: Degenerate normal -> Default
        );
        var fixture = new Fixture("custom.obj", customMesh);
        Brick brick = fixture.AddBrick(
            BrickShape.Custom,
            new BrickTextures
            {
                Default = [DEFAULT_TEX],
                Front = [FRONT_TEX],
                Back = [BACK_TEX],
            },
            meshID: "custom.obj"
        );

        Result<Mesh> result = fixture.Builder.Build(brick, BrickShape.Custom, Orientation.Identity);

        Assert.True(result.Success);
        Vector3[] uvs = result.Value.Uv;
        Assert.Equal(fixture.IndexOf(DEFAULT_TEX), uvs[0].Z);
        Assert.Equal(fixture.IndexOf(FRONT_TEX), uvs[1].Z);
        Assert.Equal(fixture.IndexOf(DEFAULT_TEX), uvs[2].Z);
    }

    [Fact]
    public void ShapedBrick_WithOnlyDefaultTextureUsesDefaultAcrossMesh()
    {
        var fixture = new Fixture("slope.obj", CreateSlopeLikeMesh());
        Brick brick = fixture.AddBrick(
            BrickShape.Any,
            new BrickTextures
            {
                Default = [DEFAULT_TEX],
            }
        );

        Result<Mesh> result = fixture.Builder.Build(brick, BrickShape.Slope, Orientation.Identity);

        Assert.True(result.Success);
        int expected = fixture.IndexOf(DEFAULT_TEX);
        Assert.All(result.Value.Uv, uv => Assert.Equal(expected, uv.Z));
    }

    [Fact]
    public void ShapedBrick_SupportsMultiVariantPerFaceTextureArrays()
    {
        var fixture = new Fixture("slope.obj", CreateSlopeLikeMesh());
        Brick brick = fixture.AddBrick(
            BrickShape.Any,
            new BrickTextures
            {
                Default = [DEFAULT_TEX],
                Front = [VARIANT_A, VARIANT_B],
            }
        );

        Result<Mesh> result = fixture.Builder.Build(brick, BrickShape.Slope, Orientation.Identity);

        Assert.True(result.Success);
        float frontLayer = result.Value.Uv[4].Z;
        Assert.True(
            frontLayer == fixture.IndexOf(VARIANT_A) || frontLayer == fixture.IndexOf(VARIANT_B),
            $"Expected front layer to resolve to one of the configured variants, got {frontLayer}."
        );
    }

    [Fact]
    public void ShippedDisplayConsole_IsShapeableAndMapsConsoleTextureToSlopeFront()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Bricks", "assets")));
        IFileParser[] parsers = [new TomlParser<BrickDefinitions>(), new LocalizedTagDefinitionParser()];
        var parser = new VirtualFileParseService(parsers, vfs);
        var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parser, vfs);
        var bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs, tags);

        Result<Brick> brickResult = bricks.Get("wb:display_console");
        Assert.True(brickResult.Success);
        Brick console = brickResult.Value;
        Assert.True(console.Shapeable);

        var albedo = new TextureArray(
            "albedo",
            [
                new Texture("missing", [255, 255, 255, 255], 1, 1, mipmaps: false),
                new Texture("display_console", [255, 255, 255, 255], 1, 1, mipmaps: false),
                new Texture("panel.0", [255, 255, 255, 255], 1, 1, mipmaps: false),
                new Texture("vent", [255, 255, 255, 255], 1, 1, mipmaps: false),
                new Texture("greeble1", [255, 255, 255, 255], 1, 1, mipmaps: false),
            ],
            mipmaps: false
        );
        var other = new TextureArray("other", [new Texture("other", [255, 255, 255, 255], 1, 1, mipmaps: false)], mipmaps: false);
        var meshes = new TestMeshDatabase();
        meshes.Meshes["slope.obj"] = CreateSlopeLikeMesh();
        var builder = new BrickPreviewMeshBuilder(bricks, bricks, new PBRTextureArrays(albedo, other, other, other, other), meshes);

        Result<Mesh> result = builder.Build(console, BrickShape.Slope, Orientation.Identity);

        Assert.True(result.Success);
        Vector3[] uvs = result.Value.Uv;
        Assert.Equal(albedo.IndexOf("greeble1"), uvs[0].Z);
        Assert.Equal(albedo.IndexOf("greeble1"), uvs[1].Z);
        Assert.Equal(albedo.IndexOf("panel.0"), uvs[2].Z);
        Assert.Equal(albedo.IndexOf("vent"), uvs[3].Z);
        Assert.Equal(albedo.IndexOf("display_console"), uvs[4].Z);
    }
}
