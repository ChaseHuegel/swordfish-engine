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
        chunkVoxel.Voxel = voxel;
        chunkVoxel.ChunkData.Palette.Increment(voxel.ID);
    }
}