using System.Collections.Generic;
using System.Numerics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Meshing;

/// <summary>Builds a combined, lit, untinted <see cref="VoxelMesh"/> for a decoded <see cref="VoxelRegionData"/>.</summary>
public sealed class RegionMeshBuilder
{
    private const int PREVIEW_LIGHT_LEVEL = 15;

    private static readonly Voxel _litAir = new(
        _ID: 0,
        _ShapeLight: new ShapeLight(default, PREVIEW_LIGHT_LEVEL),
        _OrientationFlags: 0
    );

    private readonly MeshState _meshState = new();
    private readonly MeshPostPass _meshPostPass;
    private readonly List<Int3> _occupied = [];

    /// <summary>Creates a region mesh builder over the brick, texture-layer, mesh, and optional paint databases.</summary>
    public RegionMeshBuilder(
        in IBrickDatabase brickDatabase,
        in ITextureLayerLookup textureLookup,
        in IAssetDatabase<VoxelMesh> meshDatabase,
        IPaintDatabase? paintDatabase = null
    ) {
        _meshPostPass = new MeshPostPass(_meshState, brickDatabase, textureLookup, meshDatabase, paintDatabase);
    }

    /// <summary>Builds combined untinted geometry for <paramref name="region"/> in local region coordinates.</summary>
    public Result<VoxelMesh> Build(in VoxelRegionData region)
    {
        if (region.SizeX <= 0 || region.SizeY <= 0 || region.SizeZ <= 0 || region.Chunks == null || region.Chunks.Length == 0)
        {
            return Result<VoxelMesh>.FromFailure("Region contains no geometry.");
        }

        byte sourceChunkSize = GetValidChunkSize(region.Chunks);
        var sourceObject = new VoxelObject(sourceChunkSize, region.Chunks);
        var tempObject = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);

        _occupied.Clear();
        int shift = BitOperations.TrailingZeroCount(sourceChunkSize);
        int shift2 = shift * 2;
        int mask = sourceChunkSize - 1;

        foreach (ChunkData chunk in sourceObject)
        {
            if (chunk.Palette.Only(0))
            {
                continue;
            }

            int baseX = chunk.Coords.X * sourceChunkSize;
            int baseY = chunk.Coords.Y * sourceChunkSize;
            int baseZ = chunk.Coords.Z * sourceChunkSize;
            Voxel[] voxels = chunk.Data.Voxels;

            for (var i = 0; i < voxels.Length; i++)
            {
                Voxel voxel = voxels[i];
                if (voxel.ID == 0)
                {
                    continue;
                }

                int x = baseX + (i & mask);
                int y = baseY + ((i >> shift) & mask);
                int z = baseZ + (i >> shift2);
                if ((uint)x >= (uint)region.SizeX || (uint)y >= (uint)region.SizeY || (uint)z >= (uint)region.SizeZ)
                {
                    continue;
                }

                var litVoxel = new Voxel(
                    voxel.ID,
                    new ShapeLight(voxel.GetShapeLight().Shape, PREVIEW_LIGHT_LEVEL),
                    new OrientationFlags(voxel.GetOrientation(), hasFaceOverrides: false)
                );
                tempObject.Set(x, y, z, litVoxel);
                _occupied.Add(new Int3(x, y, z));

                if (!voxel.HasFaceOverrides())
                {
                    continue;
                }

                for (var f = 0; f < 6; f++)
                {
                    var face = (BrickFace)f;
                    if (chunk.TryGetFaceOverride(i, face, out FaceOverride faceOverride) && !faceOverride.IsEmpty)
                    {
                        tempObject.SetFaceOverride(x, y, z, face.ToMask(), faceOverride);
                    }
                }
            }
        }

        if (_occupied.Count == 0)
        {
            return Result<VoxelMesh>.FromFailure("Region contains no solid bricks.");
        }

        //  Light exposed empty neighbors so exterior preview faces receive full brightness without propagating interior AO.
        for (var i = 0; i < _occupied.Count; i++)
        {
            Int3 cell = _occupied[i];
            SetAirIfEmpty(tempObject, cell.X - 1, cell.Y, cell.Z);
            SetAirIfEmpty(tempObject, cell.X + 1, cell.Y, cell.Z);
            SetAirIfEmpty(tempObject, cell.X, cell.Y - 1, cell.Z);
            SetAirIfEmpty(tempObject, cell.X, cell.Y + 1, cell.Z);
            SetAirIfEmpty(tempObject, cell.X, cell.Y, cell.Z - 1);
            SetAirIfEmpty(tempObject, cell.X, cell.Y, cell.Z + 1);
        }

        _meshState.Clear();
        foreach (ChunkData chunk in tempObject)
        {
            if (chunk.Palette.Only(0))
            {
                continue;
            }

            foreach (VoxelSample sample in chunk.GetOccupiedSampler())
            {
                _meshPostPass.Process(sample);
            }
        }

        return CombineBuckets(_meshState);
    }

    private static Result<VoxelMesh> CombineBuckets(MeshState state)
    {
        MeshState.MeshData[] buckets = [state.Opaque, state.OpaqueNoShadow, state.Masked, state.Transparent];
        var totalVertices = 0;
        var totalTriangles = 0;

        for (var b = 0; b < buckets.Length; b++)
        {
            totalVertices += buckets[b].Vertices.Count;
            totalTriangles += buckets[b].Triangles.Count;
        }

        if (totalVertices == 0 || totalTriangles == 0)
        {
            return Result<VoxelMesh>.FromFailure("Region produced no preview geometry.");
        }

        var vertices = new Vector3[totalVertices];
        var colors = new Vector4[totalVertices];
        var uv = new Vector3[totalVertices];
        var normals = new Vector3[totalVertices];
        var triangles = new uint[totalTriangles];

        var vertexBase = 0;
        var triBase = 0;

        for (var b = 0; b < buckets.Length; b++)
        {
            MeshState.MeshData bucket = buckets[b];
            int vCount = bucket.Vertices.Count;
            int tCount = bucket.Triangles.Count;
            if (vCount == 0 || tCount == 0)
            {
                continue;
            }

            for (var i = 0; i < vCount; i++)
            {
                vertices[vertexBase + i] = bucket.Vertices[i];
                colors[vertexBase + i] = bucket.Colors[i];
                uv[vertexBase + i] = bucket.UV[i];
                normals[vertexBase + i] = bucket.Normals[i];
            }

            for (var i = 0; i < tCount; i++)
            {
                triangles[triBase + i] = bucket.Triangles[i] + (uint)vertexBase;
            }

            vertexBase += vCount;
            triBase += tCount;
        }

        return Result<VoxelMesh>.FromSuccess(new VoxelMesh(triangles, vertices, colors, uv, normals));
    }

    private static void SetAirIfEmpty(VoxelObject voxelObject, int x, int y, int z)
    {
        if (voxelObject.IsResident(x, y, z) && voxelObject.Get(x, y, z).ID != 0)
        {
            return;
        }

        voxelObject.Set(x, y, z, _litAir);
    }

    private static byte GetValidChunkSize(ChunkInfo[] chunks)
    {
        byte chunkSize = chunks[0].Chunk.Size;
        if (chunkSize <= 0 || (chunkSize & (chunkSize - 1)) != 0)
        {
            return VoxelObject.DEFAULT_CHUNK_EDGE;
        }

        return chunkSize;
    }
}
