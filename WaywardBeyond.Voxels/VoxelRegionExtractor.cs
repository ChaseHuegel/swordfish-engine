using System;
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

        for (int z = minZ; z <= maxZ; z++)
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            if (!source.IsResident(x, y, z))
            {
                continue;
            }

            Voxel voxel = source.Get(x, y, z);
            if (voxel.ID == 0)
            {
                continue;
            }

            int dx = x - minX;
            int dy = y - minY;
            int dz = z - minZ;

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
                if (source.TryGetFaceOverride(x, y, z, face, out FaceOverride faceOverride) && !faceOverride.IsEmpty)
                {
                    target.SetFaceOverride(dx, dy, dz, face.ToMask(), faceOverride);
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
