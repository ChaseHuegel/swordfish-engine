using System;
using System.Collections.Generic;
using WaywardBeyond.Data;

namespace WaywardBeyond.Voxels;

/// <summary>Applies, clears, and instantiates <see cref="VoxelRegionData"/> regions on <see cref="VoxelObject"/> containers.</summary>
public static class VoxelRegionApplier
{
    /// <summary>Writes <paramref name="region"/> into <paramref name="target"/> at <paramref name="origin"/> and returns the number of solid bricks written or cleared.</summary>
    public static int ApplyTo(
        VoxelObject target,
        in VoxelRegionData region,
        Int3 origin,
        bool overwriteAir,
        ISet<Short3>? dirtyChunks = null
    )
    {
        if (region.SizeX <= 0 || region.SizeY <= 0 || region.SizeZ <= 0 || region.Chunks == null)
        {
            return 0;
        }

        byte sourceChunkSize = GetValidChunkSize(region.Chunks);
        var sourceObject = new VoxelObject(sourceChunkSize, region.Chunks);
        var written = 0;

        if (overwriteAir)
        {
            int minX = origin.X;
            int minY = origin.Y;
            int minZ = origin.Z;
            int maxX = origin.X + region.SizeX - 1;
            int maxY = origin.Y + region.SizeY - 1;
            int maxZ = origin.Z + region.SizeZ - 1;

            byte targetChunkSize = target.ChunkSize;
            int targetShift = BitOperationsShift(targetChunkSize);
            int targetShift2 = targetShift * 2;

            foreach (ChunkData chunk in target)
            {
                if (chunk.Palette.Only(0))
                {
                    continue;
                }

                int bx = chunk.Coords.X * targetChunkSize;
                int by = chunk.Coords.Y * targetChunkSize;
                int bz = chunk.Coords.Z * targetChunkSize;
                if (bx + targetChunkSize - 1 < minX || bx > maxX
                    || by + targetChunkSize - 1 < minY || by > maxY
                    || bz + targetChunkSize - 1 < minZ || bz > maxZ)
                {
                    continue;
                }

                int startX = Math.Max(0, minX - bx);
                int endX = Math.Min(targetChunkSize - 1, maxX - bx);
                int startY = Math.Max(0, minY - by);
                int endY = Math.Min(targetChunkSize - 1, maxY - by);
                int startZ = Math.Max(0, minZ - bz);
                int endZ = Math.Min(targetChunkSize - 1, maxZ - bz);
                Voxel[] voxels = chunk.Data.Voxels;

                for (int lz = startZ; lz <= endZ; lz++)
                {
                    int tz = bz + lz;
                    int sz = tz - origin.Z;
                    int zIndex = lz << targetShift2;
                    for (int ly = startY; ly <= endY; ly++)
                    {
                        int ty = by + ly;
                        int sy = ty - origin.Y;
                        int yzIndex = zIndex | (ly << targetShift);
                        for (int lx = startX; lx <= endX; lx++)
                        {
                            Voxel existing = voxels[yzIndex | lx];
                            if (existing.ID == 0 && !existing.HasFaceOverrides())
                            {
                                continue;
                            }

                            int tx = bx + lx;
                            int sx = tx - origin.X;
                            if (sourceObject.IsResident(sx, sy, sz) && sourceObject.Get(sx, sy, sz).ID != 0)
                            {
                                continue;
                            }

                            target.Set(tx, ty, tz, new Voxel());
                            dirtyChunks?.AddRange(tx, ty, tz, targetChunkSize);
                            if (existing.ID != 0)
                            {
                                written++;
                            }
                        }
                    }
                }
            }
        }

        foreach (ChunkData chunk in sourceObject)
        {
            if (chunk.Palette.Only(0))
            {
                continue;
            }

            int baseX = chunk.Coords.X * sourceChunkSize;
            int baseY = chunk.Coords.Y * sourceChunkSize;
            int baseZ = chunk.Coords.Z * sourceChunkSize;
            int shift = BitOperationsShift(sourceChunkSize);
            int shift2 = shift * 2;
            int mask = sourceChunkSize - 1;
            Voxel[] voxels = chunk.Data.Voxels;

            for (var i = 0; i < voxels.Length; i++)
            {
                Voxel srcVoxel = voxels[i];
                if (srcVoxel.ID == 0)
                {
                    continue;
                }

                int sx = baseX + (i & mask);
                int sy = baseY + ((i >> shift) & mask);
                int sz = baseZ + (i >> shift2);
                if ((uint)sx >= (uint)region.SizeX || (uint)sy >= (uint)region.SizeY || (uint)sz >= (uint)region.SizeZ)
                {
                    continue;
                }

                int tx = origin.X + sx;
                int ty = origin.Y + sy;
                int tz = origin.Z + sz;
                WriteVoxelWithOverrides(sourceObject, sx, sy, sz, in srcVoxel, target, tx, ty, tz, dirtyChunks);
                written++;
            }
        }

        return written;
    }

    /// <summary>Clears all solid voxels in the inclusive box between <paramref name="cornerA"/> and <paramref name="cornerB"/> and returns the count removed.</summary>
    public static int Clear(
        VoxelObject target,
        Int3 cornerA,
        Int3 cornerB,
        ISet<Short3>? dirtyChunks = null
    )
    {
        int minX = Math.Min(cornerA.X, cornerB.X);
        int minY = Math.Min(cornerA.Y, cornerB.Y);
        int minZ = Math.Min(cornerA.Z, cornerB.Z);
        int maxX = Math.Max(cornerA.X, cornerB.X);
        int maxY = Math.Max(cornerA.Y, cornerB.Y);
        int maxZ = Math.Max(cornerA.Z, cornerB.Z);

        var cleared = 0;
        byte chunkSize = target.ChunkSize;
        int shift = BitOperationsShift(chunkSize);
        int shift2 = shift * 2;

        foreach (ChunkData chunk in target)
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
                int z = bz + lz;
                int zIndex = lz << shift2;
                for (int ly = startY; ly <= endY; ly++)
                {
                    int y = by + ly;
                    int yzIndex = zIndex | (ly << shift);
                    for (int lx = startX; lx <= endX; lx++)
                    {
                        Voxel existing = voxels[yzIndex | lx];
                        if (existing.ID == 0)
                        {
                            continue;
                        }

                        int x = bx + lx;
                        target.Set(x, y, z, new Voxel());
                        dirtyChunks?.AddRange(x, y, z, chunkSize);
                        cleared++;
                    }
                }
            }
        }

        return cleared;
    }

    /// <summary>Builds a fresh centered <see cref="VoxelObject"/> from <paramref name="region"/> for spawning a standalone structure.</summary>
    public static VoxelObject ToCenteredVoxelObject(in VoxelRegionData region, out Int3 centerOffset)
    {
        centerOffset = new Int3(-(region.SizeX / 2), -(region.SizeY / 2), -(region.SizeZ / 2));
        var voxelObject = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        ApplyTo(voxelObject, in region, centerOffset, overwriteAir: false);
        return voxelObject;
    }

    /// <summary>Returns true when <paramref name="voxelObject"/> contains at least one non-empty voxel.</summary>
    public static bool HasSolidVoxels(VoxelObject voxelObject)
    {
        foreach (ChunkData chunk in voxelObject)
        {
            if (!chunk.Palette.Only(0))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Counts all solid voxels inside <paramref name="region"/>.</summary>
    public static int CountSolidVoxels(in VoxelRegionData region)
    {
        if (region.Chunks == null || region.Chunks.Length == 0 || region.SizeX <= 0 || region.SizeY <= 0 || region.SizeZ <= 0)
        {
            return 0;
        }

        var count = 0;
        byte chunkSize = GetValidChunkSize(region.Chunks);
        int shift = BitOperationsShift(chunkSize);
        int shift2 = shift * 2;
        int mask = chunkSize - 1;

        for (var c = 0; c < region.Chunks.Length; c++)
        {
            ChunkInfo info = region.Chunks[c];
            Voxel[]? voxels = info.Chunk.Voxels;
            if (voxels == null || info.Chunk.Size != chunkSize)
            {
                continue;
            }

            int baseX = info.OffsetX * chunkSize;
            int baseY = info.OffsetY * chunkSize;
            int baseZ = info.OffsetZ * chunkSize;

            for (var i = 0; i < voxels.Length; i++)
            {
                if (voxels[i].ID == 0)
                {
                    continue;
                }

                int x = baseX + (i & mask);
                int y = baseY + ((i >> shift) & mask);
                int z = baseZ + (i >> shift2);
                if ((uint)x < (uint)region.SizeX && (uint)y < (uint)region.SizeY && (uint)z < (uint)region.SizeZ)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>Adds the chunk containing (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) and any orthogonal boundary neighbors to <paramref name="dirtyChunks"/>.</summary>
    public static void MarkAffectedChunks(int x, int y, int z, byte chunkSize, ISet<Short3> dirtyChunks)
    {
        dirtyChunks.AddRange(x, y, z, chunkSize);
    }

    private static void WriteVoxelWithOverrides(
        VoxelObject sourceObject,
        int sx,
        int sy,
        int sz,
        in Voxel srcVoxel,
        VoxelObject target,
        int tx,
        int ty,
        int tz,
        ISet<Short3>? dirtyChunks
    )
    {
        var clean = new Voxel(
            srcVoxel.ID,
            new ShapeLight(srcVoxel.GetShapeLight().Shape, lightLevel: 0),
            new OrientationFlags(srcVoxel.OrientationFlags).WithFaceOverrides(false)
        );
        target.Set(tx, ty, tz, clean);

        if (srcVoxel.HasFaceOverrides())
        {
            for (var f = 0; f < 6; f++)
            {
                var face = (BrickFace)f;
                if (sourceObject.TryGetFaceOverride(sx, sy, sz, face, out FaceOverride faceOverride) && !faceOverride.IsEmpty)
                {
                    target.SetFaceOverride(tx, ty, tz, face.ToMask(), faceOverride);
                }
            }
        }

        dirtyChunks?.AddRange(tx, ty, tz, target.ChunkSize);
    }

    private static void AddRange(this ISet<Short3> dirtyChunks, int x, int y, int z, byte chunkSize)
    {
        int chunkMask = chunkSize - 1;
        int chunkShift = BitOperationsShift(chunkSize);
        var chunk = new Short3((short)(x >> chunkShift), (short)(y >> chunkShift), (short)(z >> chunkShift));
        dirtyChunks.Add(chunk);

        int u = x & chunkMask;
        int v = y & chunkMask;
        int w = z & chunkMask;

        if (u == 0 && chunk.X > short.MinValue) dirtyChunks.Add(new Short3((short)(chunk.X - 1), chunk.Y, chunk.Z));
        if (u == chunkMask && chunk.X < short.MaxValue) dirtyChunks.Add(new Short3((short)(chunk.X + 1), chunk.Y, chunk.Z));
        if (v == 0 && chunk.Y > short.MinValue) dirtyChunks.Add(new Short3(chunk.X, (short)(chunk.Y - 1), chunk.Z));
        if (v == chunkMask && chunk.Y < short.MaxValue) dirtyChunks.Add(new Short3(chunk.X, (short)(chunk.Y + 1), chunk.Z));
        if (w == 0 && chunk.Z > short.MinValue) dirtyChunks.Add(new Short3(chunk.X, chunk.Y, (short)(chunk.Z - 1)));
        if (w == chunkMask && chunk.Z < short.MaxValue) dirtyChunks.Add(new Short3(chunk.X, chunk.Y, (short)(chunk.Z + 1)));
    }

    private static int BitOperationsShift(byte chunkSize)
    {
        return System.Numerics.BitOperations.TrailingZeroCount(chunkSize);
    }

    private static byte GetValidChunkSize(ChunkInfo[] chunks)
    {
        byte chunkSize = chunks.Length > 0 ? chunks[0].Chunk.Size : (byte)0;
        if (chunkSize <= 0 || (chunkSize & (chunkSize - 1)) != 0)
        {
            return VoxelObject.DEFAULT_CHUNK_EDGE;
        }

        return chunkSize;
    }
}
