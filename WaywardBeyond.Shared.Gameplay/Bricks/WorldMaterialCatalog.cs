using WaywardBeyond.Shared.Bricks;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// Resolves the block-shaped, non-luminous voxel for a brick a worldgen places. Every material resolves
/// through the caller's <see cref="IBrickIdMap"/>, so worldgen emits the same ids the loaded brick
/// content assigns. The material names worldgen selects are a gameplay choice, not a brick catalog.
/// </summary>
public static class WorldMaterialCatalog
{
    /// <summary>
    ///     Returns the block-shaped, non-luminous voxel for a named brick material over the provided map.
    /// </summary>
    public static Voxel FromName(string name, in IBrickIdMap brickIdMap)
    {
        return new Voxel(brickIdMap.Id(name), _ShapeLight: 0, _Orientation: 0);
    }
}