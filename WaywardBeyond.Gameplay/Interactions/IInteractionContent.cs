using WaywardBeyond.Bricks;
using WaywardBeyond.Data;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>A type for resolving brick related items.</summary>
public interface IInteractionContent
{
    //  TODO update to use the result pattern
    /// <summary>Attempts to resolve an item to a placeable <see cref="Brick"/>, if it has one.</summary>
    bool TryGetPlaceable(string? itemID, out Brick? placeable);

    //  TODO update to use the result pattern
    /// <summary>Attempts to resolve a brick's data id to the item stack granted on break.</summary>
    bool TryGetLoot(ushort brickDataID, out ItemData loot);
}