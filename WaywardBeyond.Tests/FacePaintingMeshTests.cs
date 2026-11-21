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
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Tests per-face paint textures, color tints, connected-texture adjacency, and mesh-only dirty tracking.</summary>
public class FacePaintingMeshTests
{
    private sealed class StubBrickDatabase : IBrickDatabase
    {
        private readonly Dictionary<ushort, Brick> _byId = [];

        public void Add(ushort id, Brick brick) => _byId[id] = brick;

        public Result<Brick> Get(string id)
        {
            foreach (Brick b in _byId.Values)
            {
                if (b.ID == id) return Result<Brick>.FromSuccess(b);
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
                if (p.ID == id) return Result<Paint>.FromSuccess(p);
            }
            return Result<Paint>.FromFailure((string?)null);
        }

        public Result<Paint> Get(ushort id) =>
            _byId.TryGetValue(id, out Paint? p) ? Result<Paint>.FromSuccess(p) : Result<Paint>.FromFailure((string?)null);

        public List<Paint> Get(Func<Paint, bool> predicate) => [.. _byId.Values.Where(predicate)];
    }

    private sealed class StubMeshDatabase : IAssetDatabase<Mesh>
    {
        private readonly Mesh _unitSlope;

        public StubMeshDatabase()
        {
            //  Two vertices: one facing Front (+Z), one facing Back (-Z).
            _unitSlope = new Mesh(
                triangles: [0, 1, 0],
                vertices: [new Vector3(0, 0, 0.5f), new Vector3(0, 0, -0.5f)],
                colors: [Vector4.One, Vector4.One],
                uv: [new Vector3(0, 0, 0), new Vector3(0, 0, 0)],
                normals: [Vector3.UnitZ, -Vector3.UnitZ]
            );
        }

        public Result<Mesh> Get(string id) => Result<Mesh>.FromSuccess(_unitSlope);
    }

    private static (MeshState state, MeshPostPass pass, TextureArray albedo, StubBrickDatabase bricks, StubPaintDatabase paints) CreateHarness()
    {
        var texList = new List<Texture> { CreateTex("missing"), CreateTex("rock"), CreateTex("caution") };
        for (var i = 0; i < 16; i++)
        {
            texList.Add(CreateTex($"panel.{i}"));
        }

        var albedo = new TextureArray("albedo", [.. texList], mipmaps: false);
        var arrays = new PBRTextureArrays(albedo, albedo, albedo, albedo, albedo);
        var bricks = new StubBrickDatabase();
        var paints = new StubPaintDatabase();

        bricks.Add(1, new Brick("wb:rock", BrickAlpha.Opaque, false, null, BrickShape.Any, new BrickTextures { Default = ["rock"] }, ["paintable"]));
        bricks.Add(2, new Brick("wb:panel", BrickAlpha.Opaque, false, null, BrickShape.Any, new BrickTextures { Connected = true, Default = ["panel.0"] }, ["paintable"]));

        paints.Add(1, Paint.CreateTexture("wb:panel", "panel", BrickAlpha.Opaque, connected: true, ["panel.0"]));
        paints.Add(2, Paint.CreateTexture("wb:caution", "caution", BrickAlpha.Opaque, connected: false, ["caution"]));
        paints.Add(3, Paint.CreateColor("wb:red", "red", new Vector3(1f, 0f, 0f)));

        var state = new MeshState();
        var pass = new MeshPostPass(state, bricks, arrays, new StubMeshDatabase(), paints);
        return (state, pass, albedo, bricks, paints);
    }

    private static Texture CreateTex(string name) => new(name, [255, 255, 255, 255], 1, 1, mipmaps: false);

    [Fact]
    public void CubeFaceOverrideAppliesTextureAndLinearTintPerFace()
    {
        var (state, pass, albedo, _, paints) = CreateHarness();
        var voxels = new VoxelObject(8);
        voxels.Set(2, 2, 2, new Voxel(1, new ShapeLight(BrickShape.Block, 15), 0));
        voxels.SetFaceOverride(2, 2, 2, BrickFaceMask.Front, new FaceOverride(texturePaintId: 2, colorPaintId: 3));

        RunPass(voxels, pass);

        int cautionLayer = albedo.IndexOf("caution");
        int rockLayer = albedo.IndexOf("rock");
        Vector3 redLinear = paints.Get(3).Value.LinearColor;

        var frontFound = false;
        var backFound = false;
        for (var i = 0; i < state.Opaque.Normals.Count; i++)
        {
            if (state.Opaque.Normals[i] == Vector3.UnitZ)
            {
                frontFound = true;
                Assert.Equal(cautionLayer, (int)state.Opaque.UV[i].Z);
                Assert.Equal(redLinear.X, state.Opaque.Colors[i].X, precision: 3);
                Assert.Equal(redLinear.Y, state.Opaque.Colors[i].Y, precision: 3);
                Assert.Equal(redLinear.Z, state.Opaque.Colors[i].Z, precision: 3);
            }
            else if (state.Opaque.Normals[i] == -Vector3.UnitZ)
            {
                backFound = true;
                Assert.Equal(rockLayer, (int)state.Opaque.UV[i].Z);
                Assert.Equal(1f, state.Opaque.Colors[i].X, precision: 3);
                Assert.Equal(1f, state.Opaque.Colors[i].Y, precision: 3);
                Assert.Equal(1f, state.Opaque.Colors[i].Z, precision: 3);
            }
        }

        Assert.True(frontFound);
        Assert.True(backFound);
    }

    [Fact]
    public void UnresolvedExtensionPaintFallsBackToMissingLayer()
    {
        var (state, pass, _, _, _) = CreateHarness();
        var voxels = new VoxelObject(8);
        voxels.Set(2, 2, 2, new Voxel(1, new ShapeLight(BrickShape.Block, 15), 0));
        voxels.SetFaceOverride(2, 2, 2, BrickFaceMask.Top, new FaceOverride(texturePaintId: 999, colorPaintId: 998));

        RunPass(voxels, pass);

        var topFound = false;
        for (var i = 0; i < state.Opaque.Normals.Count; i++)
        {
            if (state.Opaque.Normals[i] == Vector3.UnitY)
            {
                topFound = true;
                Assert.Equal(0, (int)state.Opaque.UV[i].Z);
                Assert.Equal(1f, state.Opaque.Colors[i].X, precision: 3);
            }
        }

        Assert.True(topFound);
    }

    [Fact]
    public void ConnectedTexturesConnectAcrossNativeBrickAndPaintedFaceAndBreakOnDifferentPaint()
    {
        var (state, pass, albedo, _, _) = CreateHarness();
        var voxels = new VoxelObject(8);
        //  (2,2,2) is native wb:panel; (3,2,2) is wb:rock painted with wb:panel on Front (+Z).
        voxels.Set(2, 2, 2, new Voxel(2, new ShapeLight(BrickShape.Block, 15), 0));
        voxels.Set(3, 2, 2, new Voxel(1, new ShapeLight(BrickShape.Block, 15), 0));
        voxels.SetFaceTexture(3, 2, 2, BrickFaceMask.Front, texturePaintId: 1);

        RunPass(voxels, pass);

        int panelBase = albedo.IndexOf("panel.0");
        //  For Front face: Right neighbor sets bit 4 on (2,2,2); Left neighbor sets bit 2 on (3,2,2).
        var layers = new HashSet<int>();
        for (var i = 0; i < state.Opaque.Normals.Count; i++)
        {
            if (state.Opaque.Normals[i] == Vector3.UnitZ)
            {
                layers.Add((int)state.Opaque.UV[i].Z);
            }
        }

        Assert.Contains(panelBase + 4, layers);
        Assert.Contains(panelBase + 2, layers);

        //  Painting (3,2,2)'s Front face with non-connected caution breaks the connection on (2,2,2).
        voxels.SetFaceTexture(3, 2, 2, BrickFaceMask.Front, texturePaintId: 2);
        state.Clear();
        RunPass(voxels, pass);

        layers.Clear();
        for (var i = 0; i < state.Opaque.Normals.Count; i++)
        {
            if (state.Opaque.Normals[i] == Vector3.UnitZ)
            {
                layers.Add((int)state.Opaque.UV[i].Z);
            }
        }

        Assert.Contains(panelBase, layers);
        Assert.DoesNotContain(panelBase + 4, layers);
    }

    [Fact]
    public void NonBlockShapeAppliesPerFaceTextureAndTint()
    {
        var (state, pass, albedo, _, paints) = CreateHarness();
        var voxels = new VoxelObject(8);
        voxels.Set(2, 2, 2, new Voxel(1, new ShapeLight(BrickShape.Slope, 15), 0));
        voxels.SetFaceOverride(2, 2, 2, BrickFaceMask.Front, new FaceOverride(texturePaintId: 2, colorPaintId: 3));

        RunPass(voxels, pass);

        Assert.Equal(albedo.IndexOf("caution"), (int)state.Opaque.UV[0].Z);
        Assert.Equal(paints.Get(3).Value.LinearColor.X, state.Opaque.Colors[0].X, precision: 3);
        Assert.Equal(albedo.IndexOf("rock"), (int)state.Opaque.UV[1].Z);
        Assert.Equal(1f, state.Opaque.Colors[1].X, precision: 3);
    }

    [Fact]
    public void MarkFaceModifiedMarksMeshOnlyAndIncludesBoundaryNeighbor()
    {
        var registry = new VoxelChunkRegistry();
        registry.MarkFaceModified(15, 4, 4, chunkSize: 16);

        Assert.Contains(new Short3(0, 0, 0), registry.DirtyChunks);
        Assert.Contains(new Short3(1, 0, 0), registry.DirtyChunks);
        Assert.Empty(registry.GeometryDirtyChunks);
        Assert.Contains(new Short3(0, 0, 0), registry.LightDirtyChunks);
        Assert.Contains(new Short3(1, 0, 0), registry.LightDirtyChunks);
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
