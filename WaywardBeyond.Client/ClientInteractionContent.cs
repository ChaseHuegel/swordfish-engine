using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Networking.Components;

namespace WaywardBeyond.Client;

/// <inheritdoc/>
internal sealed class ClientInteractionContent(
    IAssetDatabase<Item> itemDatabase,
    IBrickDatabase brickDatabase
) : IInteractionContent
{
    private const int DEFAULT_STACK_SIZE = 100;

    /// <inheritdoc/>
    public bool TryGetPlaceable(string? itemID, out Brick? placeable)
    {
        placeable = null;

        if (string.IsNullOrEmpty(itemID))
        {
            return false;
        }

        Result<Item> itemResult = itemDatabase.Get(itemID);
        if (!itemResult.Success || itemResult.Value.Placeable == null || itemResult.Value.Placeable.Value.Type != PlaceableType.Brick)
        {
            return false;
        }

        string brickID = itemResult.Value.Placeable.Value.ID;
        Result<Brick> brickResult = brickDatabase.Get(brickID);
        if (!brickResult.Success)
        {
            return false;
        }

        placeable = brickResult.Value;
        return true;
    }

    /// <inheritdoc/>
    public bool TryGetLoot(ushort brickDataID, out ItemData loot)
    {
        loot = default;

        Result<Brick> brickResult = brickDatabase.Get(brickDataID);
        if (!brickResult.Success)
        {
            return false;
        }

        loot = InventoryComponent.Stack(brickResult.Value.ID, DEFAULT_STACK_SIZE);
        return true;
    }
}