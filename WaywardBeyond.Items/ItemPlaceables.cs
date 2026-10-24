using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;

namespace WaywardBeyond.Items;

/// <summary>Resolves the content an item refers to, shared by the client and the server.</summary>
public static class ItemPlaceables
{
    /// <summary>
    /// Resolves the brick an item places. Fails when the item is unknown, has no placeable, or the
    /// placeable is not a brick.
    /// </summary>
    public static Result<Brick> ResolvePlaceable(IAssetDatabase<Item> items, IBrickDatabase bricks, string? itemID)
    {
        if (string.IsNullOrEmpty(itemID))
        {
            return Result<Brick>.FromFailure((string?)null);
        }

        Result<Item> itemResult = items.Get(itemID);
        if (!itemResult.Success)
        {
            return new Result<Brick>(success: false, null!, itemResult.Message, itemResult.Exception);
        }

        PlaceableDefinition? placeable = itemResult.Value.Placeable;
        if (placeable == null || placeable.Value.Type != PlaceableType.Brick)
        {
            return Result<Brick>.FromFailure((string?)null);
        }

        Result<Brick> brickResult = bricks.Get(placeable.Value.ID);
        if (!brickResult.Success)
        {
            return new Result<Brick>(success: false, null!, brickResult.Message, brickResult.Exception);
        }

        return Result<Brick>.FromSuccess(brickResult.Value);
    }
}
