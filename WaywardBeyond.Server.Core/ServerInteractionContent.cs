using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Bricks;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// Headless interaction content for the dedicated server: break loot resolves from the brick databases
/// alone. Place content (the item-to-brick mapping) lives with the client's item assets, so a dedicated
/// server validates place interactions as unresolvable until shared item content exists - breaks and
/// loot grants are fully functional.
/// </summary>
public sealed class ServerInteractionContent : IInteractionContent
{
    private const int DEFAULT_STACK_SIZE = 100;

    private readonly IBrickDatabase _brickLookup;

    public ServerInteractionContent(in IBrickDatabase brickLookup)
    {
        _brickLookup = brickLookup;
    }

    public bool TryGetPlaceable(string? itemID, out PlaceableBrick placeable)
    {
        //  No shared item database: the dedicated server cannot map an item id to a brick.
        placeable = default;
        return false;
    }

    public bool TryGetLoot(ushort brickDataID, out ItemData loot)
    {
        loot = default;

        Result<BrickInfo> brickResult = _brickLookup.Get(brickDataID);
        if (!brickResult.Success)
        {
            return false;
        }

        //  A broken brick grants a stack of that brick's own item, like the client-hosted handler.
        loot = InventoryComponent.Stack(brickResult.Value.ID, DEFAULT_STACK_SIZE);
        return true;
    }
}