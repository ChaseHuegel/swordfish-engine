using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Meshing;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Building;

/// <summary>Builds a tinted holographic preview <see cref="Mesh"/> for a region clipboard or blueprint.</summary>
internal sealed class RegionPreviewMeshBuilder(in RegionMeshBuilder regionMeshBuilder)
{
    private static readonly Vector4 _validTint = new(0.45f, 0.92f, 1.0f, 0.52f);
    private static readonly Vector4 _invalidTint = new(1.0f, 0.35f, 0.38f, 0.52f);

    private readonly RegionMeshBuilder _regionMeshBuilder = regionMeshBuilder;

    private bool _hasCachedBase;
    private int _cachedRevision;
    private Orientation _cachedRotation;
    private MirrorAxes _cachedMirror;
    private VoxelMesh _cachedBase;

    /// <summary>Builds a combined ghost mesh for <paramref name="region"/> in local region space, caching untinted base geometry across validity flips.</summary>
    public Result<Mesh> Build(
        in VoxelRegionData region,
        int revision,
        Orientation rotation,
        MirrorAxes mirror,
        bool valid
    ) {
        if (!_hasCachedBase || _cachedRevision != revision || _cachedRotation != rotation || _cachedMirror != mirror)
        {
            Result<VoxelMesh> rebuilt = _regionMeshBuilder.Build(in region);
            if (!rebuilt.Success)
            {
                _hasCachedBase = false;
                return Result<Mesh>.FromFailure(rebuilt.Message);
            }

            _cachedBase = rebuilt.Value;
            _cachedRevision = revision;
            _cachedRotation = rotation;
            _cachedMirror = mirror;
            _hasCachedBase = true;
        }

        Vector4 tint = valid ? _validTint : _invalidTint;
        return CreateTintedMesh(in _cachedBase, tint);
    }

    /// <summary>Builds a combined ghost mesh for <paramref name="region"/> in local region space.</summary>
    public Result<Mesh> Build(in VoxelRegionData region, bool valid)
    {
        _hasCachedBase = false;
        Result<VoxelMesh> rebuilt = _regionMeshBuilder.Build(in region);
        if (!rebuilt.Success)
        {
            return Result<Mesh>.FromFailure(rebuilt.Message);
        }

        Vector4 tint = valid ? _validTint : _invalidTint;
        return CreateTintedMesh(rebuilt.Value, tint);
    }

    private static Result<Mesh> CreateTintedMesh(in VoxelMesh baseMesh, Vector4 tint)
    {
        int vCount = baseMesh.Colors.Length;
        var tintedColors = new Vector4[vCount];
        for (var i = 0; i < vCount; i++)
        {
            tintedColors[i] = baseMesh.Colors[i] * tint;
        }

        return Result<Mesh>.FromSuccess(new Mesh(baseMesh.Triangles, baseMesh.Vertices, tintedColors, baseMesh.Uv, baseMesh.Normals));
    }
}
