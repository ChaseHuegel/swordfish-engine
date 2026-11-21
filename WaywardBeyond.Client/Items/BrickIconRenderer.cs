using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Items;

/// <summary>Renders isometric 3D inventory icons for bricks using offscreen rendering.</summary>
internal sealed class BrickIconRenderer : IDisposable
{
    /// <summary>Width and height in pixels of rendered brick icons.</summary>
    public const int ICON_SIZE = 128;
    private const int ICON_MSAA_SAMPLES = 4;
    private const float FRAME_PADDING = 0.08f;

    internal static readonly Vector3 IsometricDirection = Vector3.Normalize(new Vector3(1.0f, 0.85f, 1.0f));

    private readonly ILogger<BrickIconRenderer> _logger;
    private readonly BrickPreviewMeshBuilder _meshBuilder;
    private readonly IOffscreenRenderer _offscreenRenderer;
    private readonly VoxelMaterials _materials;
    private readonly Shader? _ownedShader;

    /// <summary>Creates a brick icon renderer using the dedicated brick icon shader.</summary>
    public BrickIconRenderer(
        in ILogger<BrickIconRenderer> logger,
        in BrickPreviewMeshBuilder meshBuilder,
        in IOffscreenRenderer offscreenRenderer,
        in IFileParseService fileParseService,
        in PBRTextureArrays textureArrays
    ) {
        _logger = logger;
        _meshBuilder = meshBuilder;
        _offscreenRenderer = offscreenRenderer;
        _ownedShader = fileParseService.Parse<Shader>(AssetPaths.Shaders.At("brick_icon.glsl"));
        _materials = new VoxelMaterials(_ownedShader, textureArrays);
    }

    /// <summary>Creates a brick icon renderer over caller-supplied voxel materials for tests.</summary>
    internal BrickIconRenderer(
        in ILogger<BrickIconRenderer> logger,
        in BrickPreviewMeshBuilder meshBuilder,
        in IOffscreenRenderer offscreenRenderer,
        in VoxelMaterials materials
    ) {
        _logger = logger;
        _meshBuilder = meshBuilder;
        _offscreenRenderer = offscreenRenderer;
        _materials = materials;
    }

    /// <summary>Renders isometric icons for all provided bricks in a single batched pass.</summary>
    public Dictionary<string, Texture> RenderBricks(IReadOnlyList<Brick> bricks)
    {
        var rendered = new Dictionary<string, Texture>(bricks.Count, StringComparer.Ordinal);
        if (bricks.Count == 0)
        {
            return rendered;
        }

        var brickIds = new List<string>(bricks.Count);
        var meshes = new List<Mesh>(bricks.Count);
        var requests = new List<OffscreenRenderRequest>(bricks.Count);

        try
        {
            for (var i = 0; i < bricks.Count; i++)
            {
                Brick brick = bricks[i];
                BrickShape shape = GetIconShape(brick);
                Result<Mesh> meshResult = _meshBuilder.Build(brick, shape, Orientation.Identity);
                if (!meshResult.Success)
                {
                    _logger.LogError(meshResult, "Failed to build icon mesh for brick \"{brick}\".", brick.ID);
                    continue;
                }

                Mesh mesh = meshResult.Value;
                (Matrix4x4 view, Matrix4x4 projection) = ComputeIsometricCamera(mesh.Vertices);

                brickIds.Add(brick.ID);
                meshes.Add(mesh);
                requests.Add(new OffscreenRenderRequest(
                    name: $"brick_icon:{brick.ID}",
                    mesh: mesh,
                    material: _materials.GetMaterial(brick.Alpha),
                    renderOptions: _materials.GetRenderOptions(brick.Alpha),
                    model: Matrix4x4.Identity,
                    view: view,
                    projection: projection,
                    width: ICON_SIZE,
                    height: ICON_SIZE,
                    clearColor: Vector4.Zero,
                    samples: ICON_MSAA_SAMPLES,
                    mipmaps: true,
                    filter: TextureFilter.Linear,
                    colorSpace: ColorSpace.Srgb
                ));
            }

            if (requests.Count == 0)
            {
                return rendered;
            }

            Result<Texture>[] results = _offscreenRenderer.RenderBatchToTextures(CollectionsMarshal.AsSpan(requests));
            for (var i = 0; i < results.Length; i++)
            {
                Result<Texture> result = results[i];
                if (!result.Success)
                {
                    _logger.LogError(result, "Failed to render icon for brick \"{brick}\".", brickIds[i]);
                    continue;
                }

                rendered[brickIds[i]] = result.Value;
            }
        }
        finally
        {
            for (var i = 0; i < meshes.Count; i++)
            {
                meshes[i].Dispose();
            }
        }

        return rendered;
    }

    /// <summary>Returns the canonical shape rendered for a brick's inventory icon.</summary>
    internal static BrickShape GetIconShape(Brick brick)
    {
        return brick.Shape == BrickShape.Any ? BrickShape.Block : brick.Shape;
    }

    /// <summary>Computes an isometric orthographic camera tightly framing the given mesh vertices.</summary>
    internal static (Matrix4x4 View, Matrix4x4 Projection) ComputeIsometricCamera(
        ReadOnlySpan<Vector3> vertices,
        float padding = FRAME_PADDING
    ) {
        Vector3 min = new(float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity);

        if (vertices.Length == 0)
        {
            min = new Vector3(-0.5f);
            max = new Vector3(0.5f);
        }
        else
        {
            for (var i = 0; i < vertices.Length; i++)
            {
                min = Vector3.Min(min, vertices[i]);
                max = Vector3.Max(max, vertices[i]);
            }
        }

        Vector3 center = (min + max) * 0.5f;
        float radius = MathF.Max(Vector3.Distance(min, max) * 0.5f, 0.5f);
        Vector3 cameraPos = center + (IsometricDirection * (radius * 4.0f));
        Matrix4x4 view = Matrix4x4.CreateLookAt(cameraPos, center, Vector3.UnitY);

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;

        if (vertices.Length == 0)
        {
            AccumulateViewPoint(Vector3.Transform(min, view), ref minX, ref maxX, ref minY, ref maxY, ref minZ, ref maxZ);
            AccumulateViewPoint(Vector3.Transform(max, view), ref minX, ref maxX, ref minY, ref maxY, ref minZ, ref maxZ);
        }
        else
        {
            for (var i = 0; i < vertices.Length; i++)
            {
                AccumulateViewPoint(Vector3.Transform(vertices[i], view), ref minX, ref maxX, ref minY, ref maxY, ref minZ, ref maxZ);
            }
        }

        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;
        float halfExtent = MathF.Max(maxX - minX, maxY - minY) * 0.5f;
        if (halfExtent < 1e-4f)
        {
            halfExtent = 0.5f;
        }

        halfExtent *= 1.0f + padding;
        float zNear = MathF.Max(0.1f, -maxZ - 1.0f);
        float zFar = MathF.Max(zNear + 0.1f, -minZ + 1.0f);

        Matrix4x4 projection = CreateOrthographicOpenGL(
            centerX - halfExtent,
            centerX + halfExtent,
            centerY - halfExtent,
            centerY + halfExtent,
            zNear,
            zFar
        );

        return (view, projection);
    }

    private static void AccumulateViewPoint(
        in Vector3 p,
        ref float minX,
        ref float maxX,
        ref float minY,
        ref float maxY,
        ref float minZ,
        ref float maxZ
    ) {
        if (p.X < minX) minX = p.X;
        if (p.X > maxX) maxX = p.X;
        if (p.Y < minY) minY = p.Y;
        if (p.Y > maxY) maxY = p.Y;
        if (p.Z < minZ) minZ = p.Z;
        if (p.Z > maxZ) maxZ = p.Z;
    }

    private static Matrix4x4 CreateOrthographicOpenGL(float left, float right, float bottom, float top, float zNear, float zFar)
    {
        float invRightLeft = 1.0f / (right - left);
        float invTopBottom = 1.0f / (top - bottom);
        float invFarNear = 1.0f / (zFar - zNear);

        Matrix4x4 m = Matrix4x4.Identity;
        m.M11 = 2.0f * invRightLeft;
        m.M22 = 2.0f * invTopBottom;
        m.M33 = -2.0f * invFarNear;
        m.M41 = -(right + left) * invRightLeft;
        m.M42 = -(top + bottom) * invTopBottom;
        m.M43 = -(zFar + zNear) * invFarNear;
        return m;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownedShader == null)
        {
            return;
        }

        _materials.Opaque.Dispose();
        _materials.Masked.Dispose();
        _materials.Transparent.Dispose();
        _ownedShader.Dispose();
    }
}
