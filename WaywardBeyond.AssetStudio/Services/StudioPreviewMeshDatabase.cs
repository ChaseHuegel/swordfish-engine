using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Meshing;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Caches parsed Wavefront OBJ meshes across ordered workspace asset roots for 3D preview rendering.
/// </summary>
internal sealed class StudioPreviewMeshDatabase : IAssetDatabase<VoxelMesh>
{
    private readonly IReadOnlyList<string> _assetRoots;
    private readonly ObjParser _objParser = new();
    private readonly Dictionary<string, VoxelMesh> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Creates a mesh database resolving <c>meshes/*.obj</c> across <paramref name="assetRoots"/> in load order.
    /// </summary>
    public StudioPreviewMeshDatabase(IReadOnlyList<string> assetRoots)
    {
        _assetRoots = assetRoots;
    }

    /// <inheritdoc/>
    public Result<VoxelMesh> Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<VoxelMesh>.FromFailure("Mesh identifier is empty.");
        }

        var normalized = id.Trim().Replace('\\', '/');
        if (_cache.TryGetValue(normalized, out var cached))
        {
            return Result<VoxelMesh>.FromSuccess(cached);
        }

        var resolvedPath = FindWinningMeshFile(normalized);
        if (resolvedPath == null)
        {
            return Result<VoxelMesh>.FromFailure($"Mesh '{id}' was not found in workspace roots.");
        }

        try
        {
            var engineMesh = _objParser.Parse(new PathInfo(resolvedPath));
            var preview = new VoxelMesh(
                engineMesh.Triangles,
                engineMesh.Vertices,
                engineMesh.Colors,
                engineMesh.Uv,
                engineMesh.Normals);
            _cache[normalized] = preview;
            return Result<VoxelMesh>.FromSuccess(preview);
        }
        catch (Exception ex)
        {
            return Result<VoxelMesh>.FromFailure(ex);
        }
    }

    private string? FindWinningMeshFile(string relativeMeshId)
    {
        var subPath = relativeMeshId.Replace('/', Path.DirectorySeparatorChar);
        for (var i = _assetRoots.Count - 1; i >= 0; i--)
        {
            var candidate = Path.Combine(_assetRoots[i], "meshes", subPath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
