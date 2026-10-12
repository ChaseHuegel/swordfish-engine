using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels.Models;

namespace WaywardBeyond.Voxels;

public static class BrickExtensions
{
    /// <summary>Returns the data representation of this brick with a desired shape and optional orientation.</summary>
    public static Voxel ToVoxel(this Brick brick, BrickShape shape, IBrickRegistry registry, Orientation orientation = default)
    {
        if (shape == BrickShape.Any || (brick.Shape == BrickShape.Any && !brick.Shapeable))
        {
            //  `Any` is effectively an undefined shape. Use the default.
            shape = default;
        }
        else if (!brick.Shapeable)
        {
            //  If the brick is not shapeable, use the brick's shape regardless of what was requested.
            shape = brick.Shape;
        }
        
        if (!brick.IsOrientable(shape))
        {
            //  If the brick is not orientable, ignore any requested orientation.
           orientation = default;
        }
        
        ushort id = registry.Id(brick.ID);
        var shapeLight = new ShapeLight(shape, brick.Brightness);
        var voxel = new Voxel(id, shapeLight, orientation);

        return voxel;
    }
}