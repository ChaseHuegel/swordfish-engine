using System;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Client.Core;

/// <summary>
/// Client-side implementation of <see cref="IInteractionContent"/>: resolves the two placement/loot
/// facts the server needs from the asset databases the client already loads. The brick data id, shape,
/// shapeability, orientation tag, and brightness are lifted into the shared <see cref="PlaceableBrick"/>
/// so the server authors identical voxels to the client's presentation. The loot for a broken brick is
/// that brick's own asset id, matching the client's survival grant.
/// </summary>
internal sealed class ClientInteractionContent : IInteractionContent
{
    private const int DEFAULT_STACK_SIZE = 100;

    private readonly IAssetDatabase<Item> _itemDatabase;
    private readonly IAssetDatabase<BrickInfo> _brickDatabase;
    private readonly IBrickDatabase _brickLookup;

    public ClientInteractionContent(
        in IAssetDatabase<Item> itemDatabase,
        in IAssetDatabase<BrickInfo> brickDatabase,
        in IBrickDatabase brickLookup
    ) {
        _itemDatabase = itemDatabase;
        _brickDatabase = brickDatabase;
        _brickLookup = brickLookup;
    }

    public bool TryGetPlaceable(string? itemID, out PlaceableBrick placeable)
    {
        placeable = default;

        if (string.IsNullOrEmpty(itemID))
        {
            return false;
        }

        Result<Item> itemResult = _itemDatabase.Get(itemID);
        if (!itemResult.Success || itemResult.Value.Placeable == null || itemResult.Value.Placeable.Value.Type != PlaceableType.Brick)
        {
            return false;
        }

        string brickID = itemResult.Value.Placeable.Value.ID;
        Result<BrickInfo> brickResult = _brickDatabase.Get(brickID);
        if (!brickResult.Success)
        {
            return false;
        }

        BrickInfo brick = brickResult.Value;
        placeable = new PlaceableBrick(
            brick.DataID,
            brick.Shape,
            brick.Shapeable,
            brick.Tags.Contains("orientable"),
            brick.Brightness
        );
        return true;
    }

    public bool TryGetLoot(ushort brickDataID, out ItemData loot)
    {
        loot = default;

        Result<BrickInfo> brickResult = _brickLookup.Get(brickDataID);
        if (!brickResult.Success)
        {
            return false;
        }

        //  A broken brick grants a stack of that brick's own item, like the survival break handler.
        loot = InventoryComponent.Stack(brickResult.Value.ID, DEFAULT_STACK_SIZE);
        return true;
    }
}