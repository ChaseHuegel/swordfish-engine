using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Meshing;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Represents one positioned CPU mesh and alpha state submitted to <see cref="SceneRenderer"/>.</summary>
public readonly struct PreviewSceneMesh(
    VoxelMesh geometry,
    Matrix4x4 model,
    BrickAlpha alphaMode = BrickAlpha.Opaque,
    float alphaCutoff = 0.5f,
    bool doubleSided = false,
    bool useStandaloneTexture = false,
    PreviewTextureMode? textureMode = null,
    bool isOverlay = false
) {
    private static readonly BrickShape[] _gridShapes =
    [
        BrickShape.Block,
        BrickShape.Slab,
        BrickShape.Stair,
        BrickShape.Slope,
        BrickShape.Column,
        BrickShape.Plate,
    ];

    /// <summary>CPU triangle geometry to render.</summary>
    public readonly VoxelMesh Geometry = geometry;

    /// <summary>Local-to-world model transform for this mesh.</summary>
    public readonly Matrix4x4 Model = model;

    /// <summary>Alpha rasterization mode for this mesh.</summary>
    public readonly BrickAlpha AlphaMode = alphaMode;

    /// <summary>Alpha discard threshold applied when <see cref="AlphaMode"/> is <see cref="BrickAlpha.Masked"/>.</summary>
    public readonly float AlphaCutoff = alphaCutoff;

    /// <summary>True when back-face culling should be disabled for this mesh.</summary>
    public readonly bool DoubleSided = doubleSided;

    /// <summary>Determines how the preview shader sources albedo or vertex color for this mesh.</summary>
    public readonly PreviewTextureMode TextureMode = textureMode
        ?? (useStandaloneTexture ? PreviewTextureMode.StandaloneTexture : PreviewTextureMode.BlockAlbedoArray);

    /// <summary>True when this mesh renders in the final depth-cleared overlay pass for 3D gizmos.</summary>
    public readonly bool IsOverlay = isOverlay;

    /// <summary>True when this mesh samples the standalone 2D texture unit instead of the block albedo texture array.</summary>
    public bool UseStandaloneTexture => TextureMode == PreviewTextureMode.StandaloneTexture;

    /// <summary>Converts an engine OBJ <see cref="Mesh"/> into a <see cref="PreviewSceneMesh"/>.</summary>
    public static PreviewSceneMesh FromEngineMesh(
        Mesh mesh,
        Matrix4x4 model,
        BrickAlpha alphaMode = BrickAlpha.Opaque,
        float alphaCutoff = 0.5f,
        bool doubleSided = false,
        bool useStandaloneTexture = false,
        PreviewTextureMode? textureMode = null,
        bool isOverlay = false
    ) {
        var preview = new VoxelMesh(mesh.Triangles, mesh.Vertices, mesh.Colors, mesh.Uv, mesh.Normals);
        return new PreviewSceneMesh(preview, model, alphaMode, alphaCutoff, doubleSided, useStandaloneTexture, textureMode, isOverlay);
    }

    /// <summary>Builds a 3x2 grid of all six built-in shapes for a <see cref="BrickShape.Any"/> brick using a shared orientation.</summary>
    public static Result<IReadOnlyList<PreviewSceneMesh>> BuildShapeAnyGrid(
        BrickPreviewMeshBuilder builder,
        Brick brick,
        Orientation orientation,
        float cellSpacing = 1.95f
    ) {
        var items = new List<PreviewSceneMesh>(_gridShapes.Length);
        const int columns = 3;

        for (var i = 0; i < _gridShapes.Length; i++)
        {
            Result<VoxelMesh> built = builder.Build(brick, _gridShapes[i], orientation);
            if (!built.Success)
            {
                return Result<IReadOnlyList<PreviewSceneMesh>>.FromFailure(built.Message);
            }

            int col = i % columns;
            int row = i / columns;
            float x = (col - 1f) * cellSpacing;
            float y = (0.5f - row) * cellSpacing;
            bool doubleSided = brick.Alpha != BrickAlpha.Opaque;
            items.Add(new PreviewSceneMesh(built.Value, Matrix4x4.CreateTranslation(x, y, 0f), brick.Alpha, 0.5f, doubleSided));
        }

        return Result<IReadOnlyList<PreviewSceneMesh>>.FromSuccess(items);
    }

    /// <summary>Computes the combined world-space axis-aligned bounding box covering all <paramref name="meshes"/>.</summary>
    public static (Vector3 Min, Vector3 Max) ComputeBounds(IReadOnlyList<PreviewSceneMesh> meshes)
    {
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        var any = false;

        for (var m = 0; m < meshes.Count; m++)
        {
            PreviewSceneMesh item = meshes[m];
            Vector3[]? vertices = item.Geometry.Vertices;
            if (vertices == null || vertices.Length == 0)
            {
                continue;
            }

            for (var i = 0; i < vertices.Length; i++)
            {
                Vector3 world = Vector3.Transform(vertices[i], item.Model);
                min = Vector3.Min(min, world);
                max = Vector3.Max(max, world);
                any = true;
            }
        }

        return any ? (min, max) : (new Vector3(-0.5f), new Vector3(0.5f));
    }
}
