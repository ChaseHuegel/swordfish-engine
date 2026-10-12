using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>A type for resolving brick related items.</summary>
public interface IInteractionContent
{
    /// <summary>Resolves an item to the placeable <see cref="Brick"/> it places, if any.</summary>
    Result<Brick> GetPlaceable(string? itemID);

    /// <summary>Resolves a brick's data id to the item stack granted on break, if any.</summary>
    Result<ItemData> GetLoot(ushort brickDataID);
}