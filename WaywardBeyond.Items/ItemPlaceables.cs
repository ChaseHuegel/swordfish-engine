using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;

namespace WaywardBeyond.Items;

/// <summary>Resolves the content an item refers to, shared by the client and the server.</summary>
public static class ItemPlaceables
{
    /// <summary>Returns whether <paramref name="item"/> places a single brick.</summary>
    public static bool IsBrickPlaceable(Item? item)
    {
        return item != null
            && item.Placeable != null
            && item.Placeable.Value.Type == PlaceableType.Brick
            && !string.IsNullOrWhiteSpace(item.Placeable.Value.ID);
    }

    /// <summary>Returns whether <paramref name="item"/> places a bundled blueprint.</summary>
    public static bool IsBlueprintPlaceable(Item? item)
    {
        return item != null
            && item.Placeable != null
            && item.Placeable.Value.Type == PlaceableType.Blueprint
            && !string.IsNullOrWhiteSpace(item.Placeable.Value.ID);
    }

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

    /// <summary>
    /// Resolves the bundled blueprint ID an item places. Fails when <paramref name="item"/> is null,
    /// has no placeable, is not a blueprint placeable, or has an empty ID.
    /// </summary>
    public static Result<string> ResolveBlueprintId(Item? item)
    {
        if (!IsBlueprintPlaceable(item))
        {
            return Result<string>.FromFailure((string?)null);
        }

        return Result<string>.FromSuccess(item!.Placeable!.Value.ID);
    }

    /// <summary>
    /// Resolves the bundled blueprint ID an item places by item ID. Fails when the item is unknown,
    /// has no placeable, or the placeable is not a blueprint.
    /// </summary>
    public static Result<string> ResolveBlueprintId(IAssetDatabase<Item> items, string? itemID)
    {
        if (string.IsNullOrEmpty(itemID))
        {
            return Result<string>.FromFailure((string?)null);
        }

        Result<Item> itemResult = items.Get(itemID);
        if (!itemResult.Success)
        {
            return new Result<string>(success: false, string.Empty, itemResult.Message, itemResult.Exception);
        }

        return ResolveBlueprintId(itemResult.Value);
    }

    /// <summary>
    /// Resolves the bundled <see cref="BlueprintData"/> an item places. Fails when the item is unknown,
    /// is not a blueprint placeable, or the referenced blueprint does not exist.
    /// </summary>
    public static Result<BlueprintData> ResolveBlueprint(
        IAssetDatabase<Item> items,
        IBundledBlueprintDatabase blueprints,
        string? itemID
    )
    {
        Result<string> blueprintIdResult = ResolveBlueprintId(items, itemID);
        if (!blueprintIdResult.Success)
        {
            return new Result<BlueprintData>(success: false, default, blueprintIdResult.Message, blueprintIdResult.Exception);
        }

        return blueprints.GetBlueprint(blueprintIdResult.Value);
    }
}
