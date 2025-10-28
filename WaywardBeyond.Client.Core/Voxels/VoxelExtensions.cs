using Swordfish.Bricks;
using WaywardBeyond.Client.Core.Bricks;

namespace WaywardBeyond.Client.Core.Voxels;

public static class VoxelExtensions
{
    public static BrickData GetData(this Voxel voxel)
    {
        return new BrickData(voxel.ShapeLight);
    }

    public static BrickOrientation GetOrientation(this Voxel voxel)
    {
        return new BrickOrientation(voxel.Orientation);
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
}