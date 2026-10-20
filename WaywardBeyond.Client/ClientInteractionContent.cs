using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay.Interactions;

namespace WaywardBeyond.Client;

/// <inheritdoc/>
internal sealed class ClientInteractionContent(
    IAssetDatabase<Item> itemDatabase,
    IBrickDatabase brickDatabase
) : IInteractionContent
{
    private const int DEFAULT_STACK_SIZE = 100;

    /// <inheritdoc/>
    public Result<Brick> GetPlaceable(string? itemID)
    {
        if (string.IsNullOrEmpty(itemID))
        {
            return Result<Brick>.FromFailure(message: null);
        }

        Result<Item> itemResult = itemDatabase.Get(itemID);
        if (!itemResult.Success)
        {
            return new Result<Brick>(success: false, null!, itemResult.Message, itemResult.Exception);
        }
        
        if (!itemResult.Success || itemResult.Value.Placeable == null || itemResult.Value.Placeable.Value.Type != PlaceableType.Brick)
        {
            return Result<Brick>.FromFailure(message: null);
        }

        string brickID = itemResult.Value.Placeable.Value.ID;
        Result<Brick> brickResult = brickDatabase.Get(brickID);
        if (!brickResult.Success)
        {
            return new Result<Brick>(success: false, null!, brickResult.Message, brickResult.Exception);
        }

        return Result<Brick>.FromSuccess(brickResult.Value);
    }

    /// <inheritdoc/>
    public Result<ItemData> GetLoot(ushort brickDataID)
    {
        Result<Brick> brickResult = brickDatabase.Get(brickDataID);
        if (!brickResult.Success)
        {
            return Result<ItemData>.FromFailure(message: null);
        }

        return Result<ItemData>.FromSuccess(new ItemData(brickResult.Value.ID, 1, DEFAULT_STACK_SIZE));
    }
}