using System.Runtime.CompilerServices;
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
}