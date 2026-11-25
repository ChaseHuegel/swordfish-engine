using System;
using System.Numerics;
using WaywardBeyond.Data;

namespace WaywardBeyond.Voxels;

/// <summary>Extracts an axis-aligned box of voxels and per-face overrides into normalized local region data.</summary>
public static class VoxelRegionExtractor
{
    /// <summary>Copies all occupied voxels and face overrides in the inclusive box between <paramref name="cornerA"/> and <paramref name="cornerB"/>.</summary>
    public static VoxelRegionExtractResult Extract(VoxelObject source, Int3 cornerA, Int3 cornerB)
    {
        int minX = Math.Min(cornerA.X, cornerB.X);
        int minY = Math.Min(cornerA.Y, cornerB.Y);
        int minZ = Math.Min(cornerA.Z, cornerB.Z);
        int maxX = Math.Max(cornerA.X, cornerB.X);
        int maxY = Math.Max(cornerA.Y, cornerB.Y);
        int maxZ = Math.Max(cornerA.Z, cornerB.Z);

        int sizeX = maxX - minX + 1;
        int sizeY = maxY - minY + 1;
        int sizeZ = maxZ - minZ + 1;

        var target = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        var brickCount = 0;

        byte chunkSize = source.ChunkSize;
        int shift = BitOperations.TrailingZeroCount(chunkSize);
        int shift2 = shift * 2;

        foreach (ChunkData chunk in source)
        {
            if (chunk.Palette.Only(0))
            {
                continue;
            }

            int bx = chunk.Coords.X * chunkSize;
            int by = chunk.Coords.Y * chunkSize;
            int bz = chunk.Coords.Z * chunkSize;
            if (bx + chunkSize - 1 < minX || bx > maxX
                || by + chunkSize - 1 < minY || by > maxY
                || bz + chunkSize - 1 < minZ || bz > maxZ)
            {
                continue;
            }

            int startX = Math.Max(0, minX - bx);
            int endX = Math.Min(chunkSize - 1, maxX - bx);
            int startY = Math.Max(0, minY - by);
            int endY = Math.Min(chunkSize - 1, maxY - by);
            int startZ = Math.Max(0, minZ - bz);
            int endZ = Math.Min(chunkSize - 1, maxZ - bz);
            Voxel[] voxels = chunk.Data.Voxels;

            for (int lz = startZ; lz <= endZ; lz++)
            {
                int dz = (bz + lz) - minZ;
                int zIndex = lz << shift2;
                for (int ly = startY; ly <= endY; ly++)
                {
                    int dy = (by + ly) - minY;
                    int yzIndex = zIndex | (ly << shift);
                    for (int lx = startX; lx <= endX; lx++)
                    {
                        int index = yzIndex | lx;
                        Voxel voxel = voxels[index];
                        if (voxel.ID == 0)
                        {
                            continue;
                        }

                        int dx = (bx + lx) - minX;
                        var clean = new Voxel(
                            voxel.ID,
                            new ShapeLight(voxel.GetShapeLight().Shape, lightLevel: 0),
                            new OrientationFlags(voxel.OrientationFlags).WithFaceOverrides(false)
                        );
                        target.Set(dx, dy, dz, clean);
                        brickCount++;

                        if (!voxel.HasFaceOverrides())
                        {
                            continue;
                        }

                        for (var f = 0; f < 6; f++)
                        {
                            var face = (BrickFace)f;
                            if (chunk.TryGetFaceOverride(index, face, out FaceOverride faceOverride) && !faceOverride.IsEmpty)
                            {
                                target.SetFaceOverride(dx, dy, dz, face.ToMask(), faceOverride);
                            }
                        }
                    }
                }
            }
        }

        var region = new VoxelRegionData(
            sizeX,
            sizeY,
            sizeZ,
            target.GetChunkInfos(),
            _BrickPalette: null,
            _PaintPalette: null
        );
        return new VoxelRegionExtractResult(region, brickCount);
    }
}
