using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Building;

/// <summary>
///     Builds one isolated, textured brick mesh for the placement preview.
/// </summary>
internal sealed class BrickPreviewMeshBuilder
{
    private const int PREVIEW_LIGHT_LEVEL = 15;
    private const int CHUNK_EDGE = VoxelObject.DEFAULT_CHUNK_EDGE;
    private const int CHUNK_CENTER = CHUNK_EDGE / 2;

    private static readonly Vector3 _center = new(CHUNK_CENTER, CHUNK_CENTER, CHUNK_CENTER);
    private static readonly Vector4 _previewTint = new(1f);
    private static readonly Voxel _litAir = new(
        _ID: 0,
        _ShapeLight: new ShapeLight(default, PREVIEW_LIGHT_LEVEL),
        _Orientation: 0
    );

    private readonly IBrickRegistry _brickRegistry;
    private readonly IBrickDatabase _brickDatabase;
    private readonly MeshState _meshState = new();
    private readonly MeshPostPass _meshPostPass;

    /// <summary>Creates a builder over the client's brick registry, brick database, mesh database, and texture arrays.</summary>
    public BrickPreviewMeshBuilder(
        in IBrickRegistry brickRegistry,
        in IBrickDatabase brickDatabase,
        in PBRTextureArrays textureArrays,
        in IAssetDatabase<Mesh> meshDatabase
    ) {
        _brickRegistry = brickRegistry;
        _brickDatabase = brickDatabase;
        _meshPostPass = new MeshPostPass(_meshState, brickDatabase, textureArrays, meshDatabase);
    }

    /// <summary>Builds the preview mesh for the brick, shape, and voxel-local orientation.</summary>
    public Result<Mesh> Build(Brick brick, BrickShape shape, Orientation orientation)
    {
        Voxel voxel = brick.ToVoxel(shape, _brickRegistry, orientation);
        if (voxel.ID == 0)
        {
            return Result<Mesh>.FromFailure($"Brick '{brick.ID}' is not registered.");
        }

        var shapeLight = new ShapeLight(voxel.ShapeLight);
        voxel.ShapeLight = new ShapeLight(shapeLight.Shape, PREVIEW_LIGHT_LEVEL);

        var voxelObject = new VoxelObject(CHUNK_EDGE);
        voxelObject.Set(CHUNK_CENTER, CHUNK_CENTER, CHUNK_CENTER, voxel);
        SetPreviewNeighbors(voxelObject);

        _meshState.Clear();
        Short3 chunkOffset = voxelObject.ToChunkOffset(CHUNK_CENTER, CHUNK_CENTER, CHUNK_CENTER);
        if (voxelObject.TryGetChunk(chunkOffset, out ChunkData chunk))
        {
            foreach (VoxelSample sample in chunk.GetOccupiedSampler())
            {
                _meshPostPass.Process(sample);
            }
        }

        Result<Brick> brickInfoResult = _brickDatabase.Get(voxel.ID);
        BrickAlpha alpha = brickInfoResult.Success ? brickInfoResult.Value.Alpha : BrickAlpha.Opaque;
        MeshState.MeshData data = GetMeshData(alpha);
        if (data.Vertices.Count == 0)
        {
            return Result<Mesh>.FromFailure($"Brick '{brick.ID}' produced no preview geometry.");
        }

        var vertices = new Vector3[data.Vertices.Count];
        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] = data.Vertices[i] - _center;
        }

        var colors = new Vector4[data.Colors.Count];
        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = data.Colors[i] * _previewTint;
        }

        var mesh = new Mesh(
            [.. data.Triangles],
            vertices,
            colors,
            [.. data.UV],
            [.. data.Normals]
        );

        return Result<Mesh>.FromSuccess(mesh);
    }

    private MeshState.MeshData GetMeshData(BrickAlpha alpha)
    {
        return alpha switch
        {
            BrickAlpha.Masked => _meshState.Masked,
            BrickAlpha.Blend => _meshState.Transparent,
            _ => _meshState.Opaque,
        };
    }

    private static void SetPreviewNeighbors(VoxelObject voxelObject)
    {
        int x = CHUNK_CENTER;
        int y = CHUNK_CENTER;
        int z = CHUNK_CENTER;

        voxelObject.Set(x - 1, y, z, _litAir);
        voxelObject.Set(x + 1, y, z, _litAir);
        voxelObject.Set(x, y - 1, z, _litAir);
        voxelObject.Set(x, y + 1, z, _litAir);
        voxelObject.Set(x, y, z - 1, _litAir);
        voxelObject.Set(x, y, z + 1, _litAir);
    }
}
