using Swordfish.Bricks;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels;

public static class VoxelExtensions
{
    public static ShapeLight GetShapeLight(this Voxel voxel)
    {
        return new ShapeLight(voxel.ShapeLight);
    }
    
    public static BrickOrientation GetOrientation(this Voxel voxel)
    {
        return new BrickOrientation(voxel.Orientation);
    }
    
    public static BrickShape GetShape(this Voxel voxel)
    {
        return voxel.GetShapeLight().Shape;
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
    
    public static void Set(this ref ChunkVoxel chunkVoxel, ushort id, BrickOrientation orientation)
    {
        Set(ref chunkVoxel, id, shapeLight: 0, orientation.ToByte());
    }
    
    public static void Set(this ref ChunkVoxel chunkVoxel, ushort id, ShapeLight shapeLight)
    {
        Set(ref chunkVoxel, id, shapeLight, orientation: 0);
    }
    
    public static void Set(this ref ChunkVoxel chunkVoxel, ushort id)
    {
        Set(ref chunkVoxel, id, shapeLight: 0, orientation: 0);
    }
    
    public static void Set(this ref ChunkVoxel chunkVoxel, ushort id, byte shapeLight, byte orientation)
    {
        chunkVoxel.ChunkData.Palette.Decrement(chunkVoxel.Voxel.ID);
        chunkVoxel.Voxel.ID = id;
        chunkVoxel.Voxel.ShapeLight = shapeLight;
        chunkVoxel.Voxel.Orientation = orientation;
        chunkVoxel.ChunkData.Palette.Increment(id);
    }
    
    public static void Set(this ref ChunkVoxel chunkVoxel, in Voxel voxel)
    {
        chunkVoxel.ChunkData.Palette.Decrement(chunkVoxel.Voxel.ID);
        chunkVoxel.Voxel = voxel;
        chunkVoxel.ChunkData.Palette.Increment(voxel.ID);
    }
}