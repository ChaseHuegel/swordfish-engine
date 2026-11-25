#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Tests geometry-aware connected-texture adjacency for built-in non-block brick shapes.</summary>
public class NonBlockConnectedTextureTests
{
    private const int EDGE_UP = 1;
    private const int EDGE_LEFT = 2;
    private const int EDGE_RIGHT = 4;
    private const int EDGE_DOWN = 8;

    private sealed class StubBrickDatabase : IBrickDatabase
    {
        private readonly Dictionary<ushort, Brick> _byId = [];

        public void Add(ushort id, Brick brick) => _byId[id] = brick;

        public Result<Brick> Get(string id)
        {
            foreach (Brick b in _byId.Values)
            {
                if (b.ID == id)
                {
                    return Result<Brick>.FromSuccess(b);
                }
            }

            return Result<Brick>.FromFailure((string?)null);
        }

        public Result<Brick> Get(ushort id) =>
            _byId.TryGetValue(id, out Brick? b) ? Result<Brick>.FromSuccess(b) : Result<Brick>.FromFailure((string?)null);

        public List<Brick> Get(Func<Brick, bool> predicate) => [.. _byId.Values.Where(predicate)];

        public bool IsCuller(in Voxel voxel, BrickShape shape) => false;
    }

    private sealed class StubPaintDatabase : IPaintDatabase
    {
        private readonly Dictionary<ushort, Paint> _byId = [];

        public void Add(ushort id, Paint paint)
        {
            paint.BindDataID(id);
            _byId[id] = paint;
        }

        public IReadOnlyList<Paint> TexturePaints => [.. _byId.Values.Where(p => p.Kind == PaintKind.Texture)];
        public IReadOnlyList<Paint> ColorPaints => [.. _byId.Values.Where(p => p.Kind == PaintKind.Color)];

        public Result<Paint> Get(string id)
        {
            foreach (Paint p in _byId.Values)
            {
                if (p.ID == id)
                {
                    return Result<Paint>.FromSuccess(p);
                }
            }

            return Result<Paint>.FromFailure((string?)null);
        }

        public Result<Paint> Get(ushort id) =>
            _byId.TryGetValue(id, out Paint? p) ? Result<Paint>.FromSuccess(p) : Result<Paint>.FromFailure((string?)null);

        public List<Paint> Get(Func<Paint, bool> predicate) => [.. _byId.Values.Where(predicate)];
    }

    private sealed class StubMeshDatabase : IAssetDatabase<Mesh>
    {
        private readonly Mesh _sixFaceMesh;
        private readonly Mesh _slopeMesh;

        public StubMeshDatabase()
        {
            //  One vertex per cardinal face: Top, Bottom, Front, Back, Left, Right.
            _sixFaceMesh = CreateProbeMesh(
                Vector3.UnitY,
                -Vector3.UnitY,
                Vector3.UnitZ,
                -Vector3.UnitZ,
                -Vector3.UnitX,
                Vector3.UnitX
            );

            //  Five vertices matching slope.obj normals: Bottom, Diagonal Front, Back, Left, Right.
            _slopeMesh = CreateProbeMesh(
                -Vector3.UnitY,
                Vector3.Normalize(new Vector3(0f, 1f, 1f)),
                -Vector3.UnitZ,
                -Vector3.UnitX,
                Vector3.UnitX
            );
        }

        public Result<Mesh> Get(string id) =>
            Result<Mesh>.FromSuccess(id == "slope.obj" ? _slopeMesh : _sixFaceMesh);

        private static Mesh CreateProbeMesh(params Vector3[] normals)
        {
            var vertices = new Vector3[normals.Length];
            var colors = new Vector4[normals.Length];
            var uv = new Vector3[normals.Length];
            var triangles = new uint[normals.Length * 3];

            for (var i = 0; i < normals.Length; i++)
            {
                vertices[i] = normals[i] * 0.25f;
                colors[i] = Vector4.One;
                uv[i] = Vector3.Zero;
                triangles[(i * 3) + 0] = (uint)i;
                triangles[(i * 3) + 1] = (uint)i;
                triangles[(i * 3) + 2] = (uint)i;
            }

            return new Mesh(triangles, vertices, colors, uv, normals);
        }
    }

    private static (MeshState state, MeshPostPass pass, int panelBase, int cautionLayer) CreateHarness()
    {
        var texList = new List<Texture> { CreateTex("missing"), CreateTex("rock"), CreateTex("caution") };
        for (var i = 0; i < 16; i++)
        {
            texList.Add(CreateTex($"panel.{i}"));
        }

        var albedo = new TextureArray("albedo", [.. texList], mipmaps: false);
        var arrays = new PBRTextureArrays(albedo, albedo, albedo, albedo, albedo, albedo);
        var bricks = new StubBrickDatabase();
        var paints = new StubPaintDatabase();

        bricks.Add(1, new Brick("wb:rock", BrickAlpha.Opaque, false, null, BrickShape.Any, new BrickTextures { Default = ["rock"] }, paintable: true));
        bricks.Add(2, new Brick("wb:panel", BrickAlpha.Opaque, false, null, BrickShape.Any, new BrickTextures { Connected = true, Default = ["panel.0"] }, paintable: true));
        bricks.Add(3, new Brick("wb:custom_panel", BrickAlpha.Opaque, false, "custom.obj", BrickShape.Custom, new BrickTextures { Connected = true, Default = ["panel.0"] }, paintable: true));

        paints.Add(1, Paint.CreateTexture("wb:panel", "panel", BrickAlpha.Opaque, connected: true, ["panel.0"]));
        paints.Add(2, Paint.CreateTexture("wb:caution", "caution", BrickAlpha.Opaque, connected: false, ["caution"]));

        var state = new MeshState();
        var pass = new MeshPostPass(state, bricks, arrays, new StubMeshDatabase(), paints);
        return (state, pass, albedo.IndexOf("panel.0"), albedo.IndexOf("caution"));
    }

    private static Texture CreateTex(string name) => new(name, [255, 255, 255, 255], 1, 1, mipmaps: false);

    [Theory]
    [InlineData(BrickShape.Slab)]
    [InlineData(BrickShape.Plate)]
    public void SlabAndPlateConnectHorizontallyAndNeverVertically(BrickShape shape)
    {
        var (state, pass, panelBase, _) = CreateHarness();
        var voxels = new VoxelObject(8);
        var voxel = new Voxel(2, new ShapeLight(shape, 15), 0);

        //  Center at (2,2,2) surrounded on all 6 cardinal sides by identical slabs/plates.
        voxels.Set(2, 2, 2, voxel);
        voxels.Set(1, 2, 2, voxel);
        voxels.Set(3, 2, 2, voxel);
        voxels.Set(2, 2, 1, voxel);
        voxels.Set(2, 2, 3, voxel);
        voxels.Set(2, 3, 2, voxel);
        voxels.Set(2, 1, 2, voxel);

        RunPass(voxels, pass);

        //  Top and Bottom connect on all 4 horizontal edges; side faces connect only Left | Right.
        Assert.Equal(panelBase + (EDGE_UP | EDGE_LEFT | EDGE_RIGHT | EDGE_DOWN), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitY));
        Assert.Equal(panelBase + (EDGE_UP | EDGE_LEFT | EDGE_RIGHT | EDGE_DOWN), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), -Vector3.UnitY));
        Assert.Equal(panelBase + (EDGE_LEFT | EDGE_RIGHT), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitZ));
        Assert.Equal(panelBase + (EDGE_LEFT | EDGE_RIGHT), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), -Vector3.UnitZ));
        Assert.Equal(panelBase + (EDGE_LEFT | EDGE_RIGHT), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), -Vector3.UnitX));
        Assert.Equal(panelBase + (EDGE_LEFT | EDGE_RIGHT), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitX));
    }

    [Fact]
    public void ColumnConnectsOnlyVerticallyOnSideFaces()
    {
        var (state, pass, panelBase, _) = CreateHarness();
        var voxels = new VoxelObject(8);
        var column = new Voxel(2, new ShapeLight(BrickShape.Column, 15), 0);

        voxels.Set(2, 2, 2, column);
        voxels.Set(2, 3, 2, column);
        voxels.Set(2, 1, 2, column);
        voxels.Set(1, 2, 2, column);
        voxels.Set(3, 2, 2, column);
        voxels.Set(2, 2, 1, column);
        voxels.Set(2, 2, 3, column);

        RunPass(voxels, pass);

        Assert.Equal(panelBase, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitY));
        Assert.Equal(panelBase, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), -Vector3.UnitY));
        Assert.Equal(panelBase + (EDGE_UP | EDGE_DOWN), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitZ));
        Assert.Equal(panelBase + (EDGE_UP | EDGE_DOWN), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), -Vector3.UnitZ));
        Assert.Equal(panelBase + (EDGE_UP | EDGE_DOWN), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), -Vector3.UnitX));
        Assert.Equal(panelBase + (EDGE_UP | EDGE_DOWN), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitX));
    }

    [Fact]
    public void SlopeConnectsOnFlushFacesOnly()
    {
        var (state, pass, panelBase, _) = CreateHarness();
        var voxels = new VoxelObject(8);
        var slope = new Voxel(2, new ShapeLight(BrickShape.Slope, 15), 0);

        voxels.Set(2, 2, 2, slope);
        voxels.Set(1, 2, 2, slope);
        voxels.Set(3, 2, 2, slope);
        voxels.Set(2, 3, 2, slope);
        voxels.Set(2, 1, 2, slope);
        voxels.Set(2, 2, 1, slope);
        voxels.Set(2, 2, 3, slope);

        RunPass(voxels, pass);

        Vector3 diagonalNormal = Vector3.Normalize(new Vector3(0f, 1f, 1f));
        Assert.Equal(panelBase + (EDGE_LEFT | EDGE_RIGHT), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), diagonalNormal));
        Assert.Equal(panelBase + (EDGE_UP | EDGE_LEFT | EDGE_RIGHT | EDGE_DOWN), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), -Vector3.UnitZ));
        Assert.Equal(panelBase + (EDGE_UP | EDGE_LEFT | EDGE_RIGHT | EDGE_DOWN), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), -Vector3.UnitY));
        Assert.Equal(panelBase, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), -Vector3.UnitX));
        Assert.Equal(panelBase, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitX));
    }

    [Fact]
    public void StairConnectsOnFlushFacesOnly()
    {
        var (state, pass, panelBase, _) = CreateHarness();
        var voxels = new VoxelObject(8);
        var stair = new Voxel(2, new ShapeLight(BrickShape.Stair, 15), 0);

        voxels.Set(2, 2, 2, stair);
        voxels.Set(1, 2, 2, stair);
        voxels.Set(3, 2, 2, stair);
        voxels.Set(2, 3, 2, stair);
        voxels.Set(2, 1, 2, stair);
        voxels.Set(2, 2, 1, stair);
        voxels.Set(2, 2, 3, stair);

        RunPass(voxels, pass);

        Assert.Equal(panelBase + (EDGE_LEFT | EDGE_RIGHT), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitY));
        Assert.Equal(panelBase + (EDGE_LEFT | EDGE_RIGHT), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitZ));
        Assert.Equal(panelBase + (EDGE_UP | EDGE_LEFT | EDGE_RIGHT | EDGE_DOWN), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), -Vector3.UnitY));
        Assert.Equal(panelBase + (EDGE_UP | EDGE_LEFT | EDGE_RIGHT | EDGE_DOWN), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), -Vector3.UnitZ));
        Assert.Equal(panelBase, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), -Vector3.UnitX));
        Assert.Equal(panelBase, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitX));
    }

    [Fact]
    public void SlopeConnectsAlongDiagonalRamp()
    {
        var (state, pass, panelBase, _) = CreateHarness();
        var voxels = new VoxelObject(8);
        var slope = new Voxel(2, new ShapeLight(BrickShape.Slope, 15), 0);

        //  Three-step diagonal ramp along (0, +1, -1) with a lower-right neighbor.
        voxels.Set(2, 1, 3, slope);
        voxels.Set(2, 2, 2, slope);
        voxels.Set(2, 3, 1, slope);
        voxels.Set(3, 2, 2, slope);

        RunPass(voxels, pass);

        Vector3 diagonalNormal = Vector3.Normalize(new Vector3(0f, 1f, 1f));
        Assert.Equal(panelBase + EDGE_UP, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 1, 3), diagonalNormal));
        Assert.Equal(panelBase + (EDGE_UP | EDGE_RIGHT | EDGE_DOWN), GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), diagonalNormal));
        Assert.Equal(panelBase + EDGE_DOWN, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 3, 1), diagonalNormal));
        Assert.Equal(panelBase + EDGE_LEFT, GetLayerForVoxelFace(state.Opaque, new Vector3(3, 2, 2), diagonalNormal));
    }

    [Fact]
    public void SlopeConnectsAlongDiagonalRampAcrossChunkBoundary()
    {
        var (state, pass, panelBase, _) = CreateHarness();
        var voxels = new VoxelObject(8);
        var slope = new Voxel(2, new ShapeLight(BrickShape.Slope, 15), 0);

        //  Crosses the Y=7 -> Y=8 chunk boundary diagonally.
        voxels.Set(2, 7, 3, slope);
        voxels.Set(2, 8, 2, slope);

        RunPass(voxels, pass);

        Vector3 diagonalNormal = Vector3.Normalize(new Vector3(0f, 1f, 1f));
        Assert.Equal(panelBase + EDGE_UP, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 7, 3), diagonalNormal));
        Assert.Equal(panelBase + EDGE_DOWN, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 8, 2), diagonalNormal));
    }

    [Theory]
    [InlineData(BrickShape.Slab)]
    [InlineData(BrickShape.Plate)]
    public void SlabAndPlateConnectAcrossDifferentYawsOnSamePlane(BrickShape shape)
    {
        var (state, pass, panelBase, _) = CreateHarness();
        var voxels = new VoxelObject(8);
        byte yaw90 = new Orientation(pitch: 0, yaw: 1, roll: 0).ToByte();

        voxels.Set(2, 2, 2, new Voxel(2, new ShapeLight(shape, 15), 0));
        voxels.Set(3, 2, 2, new Voxel(2, new ShapeLight(shape, 15), yaw90));

        RunPass(voxels, pass);

        //  (2,2,2) connects Right on Top/Bottom/Front; (3,2,2) rotated 90 deg yaw still connects its coplanar faces.
        Assert.Equal(panelBase + EDGE_RIGHT, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitY));
        Assert.Equal(panelBase + EDGE_RIGHT, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), -Vector3.UnitY));
        Assert.Equal(panelBase + EDGE_RIGHT, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitZ));
        Assert.NotEqual(panelBase, GetLayerForVoxelFace(state.Opaque, new Vector3(3, 2, 2), Vector3.UnitY));
        Assert.NotEqual(panelBase, GetLayerForVoxelFace(state.Opaque, new Vector3(3, 2, 2), Vector3.UnitZ));
    }

    [Fact]
    public void ColumnConnectsAcrossSymmetricYawAndFlip()
    {
        var (state, pass, panelBase, _) = CreateHarness();
        var voxels = new VoxelObject(8);
        byte flipped = new Orientation(pitch: 2, yaw: 1, roll: 0).ToByte();

        voxels.Set(2, 2, 2, new Voxel(2, new ShapeLight(BrickShape.Column, 15), 0));
        voxels.Set(2, 3, 2, new Voxel(2, new ShapeLight(BrickShape.Column, 15), flipped));

        RunPass(voxels, pass);

        Assert.Equal(panelBase + EDGE_UP, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitZ));
        Assert.NotEqual(panelBase, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 3, 2), Vector3.UnitZ));
    }

    [Fact]
    public void NonBlockShapesRequireCompatibleOrientationAndSameShape()
    {
        var (state, pass, panelBase, _) = CreateHarness();
        var voxels = new VoxelObject(8);
        byte wallOrientation = new Orientation(pitch: 1, yaw: 0, roll: 0).ToByte();

        //  (2,2,2) is floor Slab; (3,2,2) is wall-mounted Slab; (1,2,2) is Block.
        voxels.Set(2, 2, 2, new Voxel(2, new ShapeLight(BrickShape.Slab, 15), 0));
        voxels.Set(3, 2, 2, new Voxel(2, new ShapeLight(BrickShape.Slab, 15), wallOrientation));
        voxels.Set(1, 2, 2, new Voxel(2, new ShapeLight(BrickShape.Block, 15), 0));

        RunPass(voxels, pass);

        Assert.Equal(panelBase, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitY));
        Assert.Equal(panelBase, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitZ));
    }

    [Fact]
    public void RotatedNonBlockShapesWithMatchingOrientationConnectAlongRotatedAxes()
    {
        var (state, pass, panelBase, _) = CreateHarness();
        var voxels = new VoxelObject(8);
        //  Pitch 90 degrees rotates local Top (+Y) to world Front (+Z) and local Behind (-Z) to world Above (+Y).
        var orientation = new Orientation(pitch: 1, yaw: 0, roll: 0);
        byte orientationByte = orientation.ToByte();
        var slab = new Voxel(2, new ShapeLight(BrickShape.Slab, 15), orientationByte);

        voxels.Set(2, 2, 2, slab);
        voxels.Set(2, 3, 2, slab);

        RunPass(voxels, pass);

        Vector3 rotatedTopNormal = Vector3.Transform(Vector3.UnitY, orientation.ToQuaternion());
        Assert.Equal(panelBase + EDGE_UP, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), rotatedTopNormal));
        Assert.Equal(panelBase + EDGE_DOWN, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 3, 2), rotatedTopNormal));
    }

    [Fact]
    public void NonBlockShapesConnectAcrossNativeBrickAndPaintedFace()
    {
        var (state, pass, panelBase, cautionLayer) = CreateHarness();
        var voxels = new VoxelObject(8);
        //  (2,2,2) is native wb:panel slab; (3,2,2) is wb:rock slab painted with wb:panel on Top.
        voxels.Set(2, 2, 2, new Voxel(2, new ShapeLight(BrickShape.Slab, 15), 0));
        voxels.Set(3, 2, 2, new Voxel(1, new ShapeLight(BrickShape.Slab, 15), 0));
        voxels.SetFaceTexture(3, 2, 2, BrickFaceMask.Top, texturePaintId: 1);

        RunPass(voxels, pass);

        Assert.Equal(panelBase + EDGE_RIGHT, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitY));
        Assert.Equal(panelBase + EDGE_LEFT, GetLayerForVoxelFace(state.Opaque, new Vector3(3, 2, 2), Vector3.UnitY));

        //  Repainting (3,2,2)'s Top face with non-connected caution breaks the connection.
        voxels.SetFaceTexture(3, 2, 2, BrickFaceMask.Top, texturePaintId: 2);
        state.Clear();
        RunPass(voxels, pass);

        Assert.Equal(panelBase, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitY));
        Assert.Equal(cautionLayer, GetLayerForVoxelFace(state.Opaque, new Vector3(3, 2, 2), Vector3.UnitY));
    }

    [Fact]
    public void CustomShapeRemainsUnconnected()
    {
        var (state, pass, panelBase, _) = CreateHarness();
        var voxels = new VoxelObject(8);
        var custom = new Voxel(3, new ShapeLight(BrickShape.Custom, 15), 0);

        voxels.Set(2, 2, 2, custom);
        voxels.Set(3, 2, 2, custom);

        RunPass(voxels, pass);

        Assert.Equal(panelBase, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitY));
        Assert.Equal(panelBase, GetLayerForVoxelFace(state.Opaque, new Vector3(2, 2, 2), Vector3.UnitZ));
        Assert.Equal(panelBase, GetLayerForVoxelFace(state.Opaque, new Vector3(3, 2, 2), Vector3.UnitY));
    }

    [Fact]
    public void ConnectedGlassSlopesCullInnerTouchingSideFaces()
    {
        var texList = new List<Texture> { CreateTex("missing") };
        for (var i = 0; i < 16; i++)
        {
            texList.Add(CreateTex($"glass.{i}"));
        }

        var albedo = new TextureArray("albedo", [.. texList], mipmaps: false);
        var arrays = new PBRTextureArrays(albedo, albedo, albedo, albedo, albedo, albedo);
        var bricks = new StubBrickDatabase();
        bricks.Add(1, new Brick("wb:glass", BrickAlpha.Blend, false, null, BrickShape.Any, new BrickTextures { Connected = true, Default = ["glass.0"] }));

        var meshDb = new BoundaryMeshDatabase();
        var state = new MeshState();
        var pass = new MeshPostPass(state, bricks, arrays, meshDb);

        var voxels = new VoxelObject(8);
        var glassSlope = new Voxel(1, new ShapeLight(BrickShape.Slope, 15), 0);
        voxels.Set(1, 2, 2, glassSlope);
        voxels.Set(2, 2, 2, glassSlope);
        voxels.Set(3, 2, 2, glassSlope);

        RunPass(voxels, pass);

        //  Leftmost slope keeps Left (-X), culls Right (+X).
        Assert.True(HasFaceAtVoxel(state.Transparent, new Vector3(1, 2, 2), -Vector3.UnitX));
        Assert.False(HasFaceAtVoxel(state.Transparent, new Vector3(1, 2, 2), Vector3.UnitX));

        //  Middle slope culls both Left (-X) and Right (+X), keeps diagonal Front, Back, and Bottom.
        Assert.False(HasFaceAtVoxel(state.Transparent, new Vector3(2, 2, 2), -Vector3.UnitX));
        Assert.False(HasFaceAtVoxel(state.Transparent, new Vector3(2, 2, 2), Vector3.UnitX));
        Assert.True(HasFaceAtVoxel(state.Transparent, new Vector3(2, 2, 2), Vector3.Normalize(new Vector3(0f, 1f, 1f))));
        Assert.True(HasFaceAtVoxel(state.Transparent, new Vector3(2, 2, 2), -Vector3.UnitZ));
        Assert.True(HasFaceAtVoxel(state.Transparent, new Vector3(2, 2, 2), -Vector3.UnitY));

        //  Rightmost slope culls Left (-X), keeps Right (+X).
        Assert.False(HasFaceAtVoxel(state.Transparent, new Vector3(3, 2, 2), -Vector3.UnitX));
        Assert.True(HasFaceAtVoxel(state.Transparent, new Vector3(3, 2, 2), Vector3.UnitX));
    }

    [Fact]
    public void ConnectedGlassSlabsCullInnerSidesAcrossSymmetricYawButKeepTopAndBottom()
    {
        var texList = new List<Texture> { CreateTex("missing") };
        for (var i = 0; i < 16; i++)
        {
            texList.Add(CreateTex($"glass.{i}"));
        }

        var albedo = new TextureArray("albedo", [.. texList], mipmaps: false);
        var arrays = new PBRTextureArrays(albedo, albedo, albedo, albedo, albedo, albedo);
        var bricks = new StubBrickDatabase();
        bricks.Add(1, new Brick("wb:glass", BrickAlpha.Blend, false, null, BrickShape.Any, new BrickTextures { Connected = true, Default = ["glass.0"] }));

        var meshDb = new BoundaryMeshDatabase();
        var state = new MeshState();
        var pass = new MeshPostPass(state, bricks, arrays, meshDb);

        var voxels = new VoxelObject(8);
        byte yaw90 = new Orientation(pitch: 0, yaw: 1, roll: 0).ToByte();
        voxels.Set(2, 2, 2, new Voxel(1, new ShapeLight(BrickShape.Slab, 15), 0));
        voxels.Set(3, 2, 2, new Voxel(1, new ShapeLight(BrickShape.Slab, 15), yaw90));
        voxels.Set(2, 1, 2, new Voxel(1, new ShapeLight(BrickShape.Slab, 15), 0));

        RunPass(voxels, pass);

        //  Shared side between (2,2,2) and (3,2,2) is culled; Top (y=0) and Bottom (air gap to slab below) are kept.
        Assert.False(HasFaceAtVoxel(state.Transparent, new Vector3(2, 2, 2), Vector3.UnitX));
        Assert.False(HasFaceAtVoxel(state.Transparent, new Vector3(3, 2, 2), -Vector3.UnitX));
        Assert.True(HasFaceAtVoxel(state.Transparent, new Vector3(2, 2, 2), Vector3.UnitY));
        Assert.True(HasFaceAtVoxel(state.Transparent, new Vector3(2, 2, 2), -Vector3.UnitY));
    }

    private sealed class BoundaryMeshDatabase : IAssetDatabase<Mesh>
    {
        private readonly Mesh _slopeMesh;
        private readonly Mesh _slabMesh;

        public BoundaryMeshDatabase()
        {
            _slopeMesh = CreateBoundaryTriangles(
                (new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0.5f, -0.5f, 0.5f), Vector3.UnitX),
                (new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(-0.5f, 0.5f, -0.5f), -Vector3.UnitX),
                (new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f), -Vector3.UnitZ),
                (new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, -0.5f), -Vector3.UnitY),
                (new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f), Vector3.Normalize(new Vector3(0f, 1f, 1f)))
            );

            _slabMesh = CreateBoundaryTriangles(
                (new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), Vector3.UnitX),
                (new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, -0.5f), -Vector3.UnitX),
                (new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f), Vector3.UnitY),
                (new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, -0.5f), -Vector3.UnitY)
            );
        }

        public Result<Mesh> Get(string id) =>
            Result<Mesh>.FromSuccess(id == "slope.obj" ? _slopeMesh : _slabMesh);

        private static Mesh CreateBoundaryTriangles(params (Vector3 V0, Vector3 V1, Vector3 V2, Vector3 Normal)[] tris)
        {
            var vertices = new Vector3[tris.Length * 3];
            var normals = new Vector3[tris.Length * 3];
            var colors = new Vector4[tris.Length * 3];
            var uv = new Vector3[tris.Length * 3];
            var indices = new uint[tris.Length * 3];

            for (var t = 0; t < tris.Length; t++)
            {
                int b = t * 3;
                vertices[b + 0] = tris[t].V0;
                vertices[b + 1] = tris[t].V1;
                vertices[b + 2] = tris[t].V2;
                normals[b + 0] = tris[t].Normal;
                normals[b + 1] = tris[t].Normal;
                normals[b + 2] = tris[t].Normal;
                colors[b + 0] = Vector4.One;
                colors[b + 1] = Vector4.One;
                colors[b + 2] = Vector4.One;
                indices[b + 0] = (uint)(b + 0);
                indices[b + 1] = (uint)(b + 1);
                indices[b + 2] = (uint)(b + 2);
            }

            return new Mesh(indices, vertices, colors, uv, normals);
        }
    }

    private static bool HasFaceAtVoxel(MeshState.MeshData meshData, Vector3 voxelOrigin, Vector3 worldNormal)
    {
        for (var i = 0; i < meshData.Triangles.Count; i += 3)
        {
            int i0 = (int)meshData.Triangles[i];
            int i1 = (int)meshData.Triangles[i + 1];
            int i2 = (int)meshData.Triangles[i + 2];
            Vector3 centroid = (meshData.Vertices[i0] + meshData.Vertices[i1] + meshData.Vertices[i2]) / 3f;
            if (MathF.Abs(centroid.X - voxelOrigin.X) <= 0.51f &&
                MathF.Abs(centroid.Y - voxelOrigin.Y) <= 0.51f &&
                MathF.Abs(centroid.Z - voxelOrigin.Z) <= 0.51f &&
                Vector3.Dot(meshData.Normals[i0], worldNormal) > 0.99f)
            {
                //  Confirm the triangle belongs to voxelOrigin and not an adjacent voxel sharing the boundary plane.
                Vector3 inside = centroid - (worldNormal * 0.01f);
                if (MathF.Abs(inside.X - voxelOrigin.X) < 0.5f &&
                    MathF.Abs(inside.Y - voxelOrigin.Y) < 0.5f &&
                    MathF.Abs(inside.Z - voxelOrigin.Z) < 0.5f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int GetLayerForVoxelFace(MeshState.MeshData meshData, Vector3 voxelOrigin, Vector3 worldNormal)
    {
        for (var i = 0; i < meshData.Vertices.Count; i++)
        {
            if (Vector3.DistanceSquared(meshData.Vertices[i], voxelOrigin) < 0.25f &&
                Vector3.Dot(meshData.Normals[i], worldNormal) > 0.99f)
            {
                return (int)meshData.UV[i].Z;
            }
        }

        throw new InvalidOperationException($"No vertex found at {voxelOrigin} with normal {worldNormal}.");
    }

    private static void RunPass(VoxelObject voxels, MeshPostPass pass)
    {
        foreach (ChunkData chunk in voxels)
        {
            foreach (VoxelSample sample in chunk.GetOccupiedSampler())
            {
                pass.Process(sample);
            }
        }
    }
}
