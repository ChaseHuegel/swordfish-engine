using System.Numerics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Meshing;

/// <summary>Builds one isolated, textured <see cref="VoxelMesh"/> for a single brick.</summary>
public sealed class BrickPreviewMeshBuilder
{
    private const int PREVIEW_LIGHT_LEVEL = 15;
    private const int CHUNK_EDGE = VoxelObject.DEFAULT_CHUNK_EDGE;
    private const int CHUNK_CENTER = CHUNK_EDGE / 2;

    private static readonly Vector3 _center = new(CHUNK_CENTER, CHUNK_CENTER, CHUNK_CENTER);
    private static readonly Vector4 _previewTint = new(1f);
    private static readonly Voxel _litAir = new(
        _ID: 0,
        _ShapeLight: new ShapeLight(default, PREVIEW_LIGHT_LEVEL),
        _OrientationFlags: 0
    );

    private readonly IBrickRegistry _brickRegistry;
    private readonly IBrickDatabase _brickDatabase;
    private readonly MeshState _meshState = new();
    private readonly MeshPostPass _meshPostPass;

    /// <summary>Creates a builder over the brick registry, brick database, texture-layer lookup, and mesh database.</summary>
    public BrickPreviewMeshBuilder(
        in IBrickRegistry brickRegistry,
        in IBrickDatabase brickDatabase,
        in ITextureLayerLookup textureLookup,
        in IAssetDatabase<VoxelMesh> meshDatabase
    ) {
        _brickRegistry = brickRegistry;
        _brickDatabase = brickDatabase;
        _meshPostPass = new MeshPostPass(_meshState, brickDatabase, textureLookup, meshDatabase);
    }

    /// <summary>Builds the centered preview mesh for <paramref name="brick"/>, <paramref name="shape"/>, and <paramref name="orientation"/>.</summary>
    public Result<VoxelMesh> Build(Brick brick, BrickShape shape, Orientation orientation)
    {
        Voxel voxel = brick.ToVoxel(shape, _brickRegistry, orientation);
        if (voxel.ID == 0)
        {
            return Result<VoxelMesh>.FromFailure($"Brick '{brick.ID}' is not registered.");
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
        bool castShadows = !brickInfoResult.Success || brickInfoResult.Value.CastShadows;
        MeshState.MeshData data = GetMeshData(alpha, castShadows);
        if (data.Vertices.Count == 0)
        {
            return Result<VoxelMesh>.FromFailure($"Brick '{brick.ID}' produced no preview geometry.");
        }

        return Result<VoxelMesh>.FromSuccess(CreateMesh(data));
    }

    /// <summary>Builds a centered placeholder cube mesh using the missing-brick texture layer.</summary>
    public VoxelMesh BuildFallback()
    {
        var voxel = new Voxel(
            _ID: ushort.MaxValue,
            _ShapeLight: new ShapeLight(BrickShape.Block, PREVIEW_LIGHT_LEVEL),
            _OrientationFlags: 0
        );

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

        return CreateMesh(_meshState.Opaque);
    }

    private static VoxelMesh CreateMesh(MeshState.MeshData data)
    {
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

        return new VoxelMesh(
            [.. data.Triangles],
            vertices,
            colors,
            [.. data.UV],
            [.. data.Normals]
        );
    }

    private MeshState.MeshData GetMeshData(BrickAlpha alpha, bool castShadows)
    {
        return alpha switch
        {
            BrickAlpha.Masked => _meshState.Masked,
            BrickAlpha.Blend => _meshState.Transparent,
            _ => castShadows ? _meshState.Opaque : _meshState.OpaqueNoShadow,
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
