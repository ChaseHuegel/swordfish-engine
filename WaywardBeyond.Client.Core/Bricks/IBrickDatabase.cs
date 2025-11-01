using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Bricks;

internal interface IBrickDatabase
{
    bool IsCuller(Voxel voxel);
    bool IsCuller(Voxel voxel, ShapeLight shapeLight);
    bool IsCuller(Voxel voxel, BrickShape shape);
    
    /// <summary>
    ///     Attempts to get a brick's info by its data ID.
    /// </summary>
    Result<BrickInfo> Get(ushort id);
}