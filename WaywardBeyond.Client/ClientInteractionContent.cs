using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Items;
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
        return ItemPlaceables.ResolvePlaceable(itemDatabase, brickDatabase, itemID);
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