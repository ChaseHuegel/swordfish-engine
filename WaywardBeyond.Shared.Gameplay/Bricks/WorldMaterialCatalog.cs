using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
///     Stable voxel representations of the named materials the world generator places. The id for each
///     name is derived with the same FNV1a brick-id rule the client's brick database uses, and every
///     material here is a block-shaped (shape 0), non-luminous (light 0) voxel, which is exactly what
///     <see cref="WaywardBeyond.Client.Core.Bricks.BrickInfo.ToVoxel()"/> produces for these bricks. Keeping
///     this in game-shared code lets the authoritative server generate world data whose voxel ids resolve
///     to the correct bricks once streamed to a client.
/// </summary>
public static class WorldMaterialCatalog
{
    public static Voxel Rock => FromName("rock");

    public static Voxel Ice => FromName("ice");

    public static Voxel Core => FromName("core");

    /// <summary>
    ///     Returns the block-shaped, non-luminous voxel for a named brick material.
    /// </summary>
    public static Voxel FromName(string name)
    {
        return new Voxel(FNV1a.ComputeDataID(name), _ShapeLight: 0, _Orientation: 0);
    }
}