using System;
using System.Collections.Generic;
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
/// Builds a single combined, textured holographic preview <see cref="Mesh"/> for a region clipboard or blueprint.
/// </summary>
internal sealed class RegionPreviewMeshBuilder
{
    private const int PREVIEW_LIGHT_LEVEL = 15;

    private static readonly Vector4 _validTint = new(0.45f, 0.92f, 1.0f, 0.52f);
    private static readonly Vector4 _invalidTint = new(1.0f, 0.35f, 0.38f, 0.52f);
    private static readonly Voxel _litAir = new(
        _ID: 0,
        _ShapeLight: new ShapeLight(default, PREVIEW_LIGHT_LEVEL),
        _OrientationFlags: 0);

    private readonly MeshState _meshState = new();
    private readonly MeshPostPass _meshPostPass;

    /// <summary>Creates a preview mesh builder over the client's brick, paint, mesh, and texture databases.</summary>
    public RegionPreviewMeshBuilder(
        in IBrickDatabase brickDatabase,
        in PBRTextureArrays textureArrays,
        in IAssetDatabase<Mesh> meshDatabase,
        IPaintDatabase? paintDatabase = null
    )
    {
        _meshPostPass = new MeshPostPass(_meshState, brickDatabase, textureArrays, meshDatabase, paintDatabase);
    }

    /// <summary>Builds a combined ghost mesh for <paramref name="region"/> in local region space.</summary>
    public Result<Mesh> Build(in VoxelRegionData region, bool valid)
    {
        if (region.SizeX <= 0 || region.SizeY <= 0 || region.SizeZ <= 0 || region.Chunks == null || region.Chunks.Length == 0)
        {
            return Result<Mesh>.FromFailure("Region contains no geometry.");
        }

        byte sourceChunkSize = GetValidChunkSize(region.Chunks);
        var sourceObject = new VoxelObject(sourceChunkSize, region.Chunks);
        var tempObject = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);

        var occupied = new List<(int x, int y, int z, Voxel voxel, ChunkData srcChunk, int srcIndex)>();
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
                    new OrientationFlags(voxel.GetOrientation(), hasFaceOverrides: false));
                tempObject.Set(x, y, z, litVoxel);
                occupied.Add((x, y, z, voxel, chunk, i));

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

        if (occupied.Count == 0)
        {
            return Result<Mesh>.FromFailure("Region contains no solid bricks.");
        }

        // Light exposed empty neighbors so exterior preview faces receive full brightness without propagating interior AO.
        for (var i = 0; i < occupied.Count; i++)
        {
            (int x, int y, int z, _, _, _) = occupied[i];
            SetAirIfEmpty(tempObject, x - 1, y, z);
            SetAirIfEmpty(tempObject, x + 1, y, z);
            SetAirIfEmpty(tempObject, x, y - 1, z);
            SetAirIfEmpty(tempObject, x, y + 1, z);
            SetAirIfEmpty(tempObject, x, y, z - 1);
            SetAirIfEmpty(tempObject, x, y, z + 1);
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

        Vector4 tint = valid ? _validTint : _invalidTint;
        return CreateCombinedMesh(_meshState, tint);
    }

    private static Result<Mesh> CreateCombinedMesh(MeshState state, Vector4 tint)
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
            return Result<Mesh>.FromFailure("Region produced no preview geometry.");
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
                colors[vertexBase + i] = bucket.Colors[i] * tint;
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

        return Result<Mesh>.FromSuccess(new Mesh(triangles, vertices, colors, uv, normals));
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
