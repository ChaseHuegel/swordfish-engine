using WaywardBeyond.Data;

namespace WaywardBeyond.Voxels;

public static class VoxelExtensions
{
    public static ShapeLight GetShapeLight(this Voxel voxel)
    {
        return new ShapeLight(voxel.ShapeLight);
    }

    public static int GetLightLevel(this Voxel voxel)
    {
        return voxel.GetShapeLight().LightLevel;
    }

    /// <summary>Returns the unpacked orientation and flag bits for this voxel.</summary>
    public static OrientationFlags GetOrientationFlags(this Voxel voxel)
    {
        return new OrientationFlags(voxel.OrientationFlags);
    }

    /// <summary>Returns the 6-bit orientation of this voxel without flag bits.</summary>
    public static Orientation GetOrientation(this Voxel voxel)
    {
        return voxel.GetOrientationFlags().Orientation;
    }

    /// <summary>Returns true when this voxel carries per-face paint overrides in its chunk sidecar.</summary>
    public static bool HasFaceOverrides(this Voxel voxel)
    {
        return voxel.GetOrientationFlags().HasFaceOverrides;
    }

    public static bool HasAny(this VoxelSample sample)
    {
        return sample.Center.ID != 0 ||
               sample.Left.ID != 0 ||
               sample.Right.ID != 0 ||
               sample.Ahead.ID != 0 ||
               sample.Behind.ID != 0 ||
               sample.Above.ID != 0 ||
               sample.Below.ID != 0;
    }

    public static void Set(this ref ChunkVoxel chunkVoxel, in Voxel voxel)
    {
        chunkVoxel.ChunkData.Palette.Decrement(chunkVoxel.Voxel.ID);

        //  Overwriting or breaking a voxel clears any stale face overrides and flag bit.
        chunkVoxel.ChunkData.ClearFaceOverrides(chunkVoxel.Index);
        Voxel clean = voxel;
        clean.OrientationFlags = new OrientationFlags(clean.OrientationFlags).WithFaceOverrides(false);

        chunkVoxel.Voxel = clean;
        chunkVoxel.ChunkData.Palette.Increment(clean.ID);
        chunkVoxel.ChunkData.SetOccupied(chunkVoxel.Index, clean.ID != 0);
    }
}
