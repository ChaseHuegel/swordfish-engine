using System;
using System.Collections.Generic;
using System.Threading;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Meshing;

namespace WaywardBeyond.Client.Voxels.Building;

/// <summary>Adapts an engine <see cref="IAssetDatabase{T}"/> of <see cref="Mesh"/> to <see cref="VoxelMesh"/> for shared meshing.</summary>
internal sealed class EngineMeshPreviewDatabase(IAssetDatabase<Mesh> meshes) : IAssetDatabase<VoxelMesh>
{
    private readonly IAssetDatabase<Mesh> _meshes = meshes;
    private readonly Lock _cacheLock = new();
    private readonly Dictionary<string, VoxelMesh> _cache = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Result<VoxelMesh> Get(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Result<VoxelMesh>.FromFailure("Mesh id is empty.");
        }

        lock (_cacheLock)
        {
            if (_cache.TryGetValue(id, out VoxelMesh cached))
            {
                return Result<VoxelMesh>.FromSuccess(cached);
            }
        }

        Result<Mesh> result = _meshes.Get(id);
        if (!result.Success || result.Value == null)
        {
            return new Result<VoxelMesh>(false, default, result.Message, result.Exception);
        }

        Mesh source = result.Value;
        var converted = new VoxelMesh(source.Triangles, source.Vertices, source.Colors, source.Uv, source.Normals);
        lock (_cacheLock)
        {
            _cache[id] = converted;
        }

        return Result<VoxelMesh>.FromSuccess(converted);
    }
}
