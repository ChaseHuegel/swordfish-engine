namespace WaywardBeyond.Voxels;

/// <summary>Shared size and payload limits for region cut, copy, paste, and blueprint operations.</summary>
public static class RegionLimits
{
    /// <summary>Maximum edge length in voxels along any axis.</summary>
    public const int MAX_REGION_EDGE = 64;

    /// <summary>Maximum total bounding volume in voxels for one region operation.</summary>
    public const int MAX_REGION_VOLUME = 65_536;

    /// <summary>Maximum blueprint display name length in characters.</summary>
    public const int MAX_BLUEPRINT_NAME_LENGTH = 64;

    /// <summary>Maximum serialized region payload size in bytes.</summary>
    public const int MAX_PAYLOAD_BYTES = 4 * 1024 * 1024;

    /// <summary>Returns whether the region dimensions are positive and within configured bounds.</summary>
    public static bool IsValidSize(int sizeX, int sizeY, int sizeZ)
    {
        if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
        {
            return false;
        }

        if (sizeX > MAX_REGION_EDGE || sizeY > MAX_REGION_EDGE || sizeZ > MAX_REGION_EDGE)
        {
            return false;
        }

        return (long)sizeX * sizeY * sizeZ <= MAX_REGION_VOLUME;
    }

    /// <summary>Returns whether the inclusive min/max cell bounds are non-empty and within configured limits.</summary>
    public static bool IsValidBounds(Int3 min, Int3 max)
    {
        if (max.X < min.X || max.Y < min.Y || max.Z < min.Z)
        {
            return false;
        }

        long sizeX = (long)max.X - min.X + 1;
        long sizeY = (long)max.Y - min.Y + 1;
        long sizeZ = (long)max.Z - min.Z + 1;
        if (sizeX > MAX_REGION_EDGE || sizeY > MAX_REGION_EDGE || sizeZ > MAX_REGION_EDGE)
        {
            return false;
        }

        return sizeX * sizeY * sizeZ <= MAX_REGION_VOLUME;
    }

    /// <summary>Computes inclusive paste bounds without signed integer overflow.</summary>
    public static bool TryComputePasteMax(Int3 origin, int sizeX, int sizeY, int sizeZ, out Int3 max)
    {
        if (!IsValidSize(sizeX, sizeY, sizeZ))
        {
            max = default;
            return false;
        }

        long maxX = (long)origin.X + sizeX - 1;
        long maxY = (long)origin.Y + sizeY - 1;
        long maxZ = (long)origin.Z + sizeZ - 1;
        if (maxX < int.MinValue || maxX > int.MaxValue
            || maxY < int.MinValue || maxY > int.MaxValue
            || maxZ < int.MinValue || maxZ > int.MaxValue)
        {
            max = default;
            return false;
        }

        max = new Int3((int)maxX, (int)maxY, (int)maxZ);
        return true;
    }

    /// <summary>Returns whether inclusive bounds fit within <see cref="short"/> chunk coordinates for <paramref name="chunkSize"/>.</summary>
    public static bool IsWithinChunkRange(Int3 min, Int3 max, byte chunkSize)
    {
        if (chunkSize == 0 || max.X < min.X || max.Y < min.Y || max.Z < min.Z)
        {
            return false;
        }

        long minCoord = (long)short.MinValue * chunkSize;
        long maxCoord = ((long)short.MaxValue + 1) * chunkSize - 1;
        return min.X >= minCoord && max.X <= maxCoord
            && min.Y >= minCoord && max.Y <= maxCoord
            && min.Z >= minCoord && max.Z <= maxCoord;
    }
}
