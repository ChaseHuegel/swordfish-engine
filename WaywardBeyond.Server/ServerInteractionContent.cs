using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Items;

namespace WaywardBeyond.Server;

/// <inheritdoc/>
public sealed class ServerInteractionContent(IItemDatabase itemDatabase, IBrickDatabase brickLookup) : IInteractionContent
{
    private const int DEFAULT_STACK_SIZE = 100;

    /// <inheritdoc/>
    public Result<Brick> GetPlaceable(string? itemID)
    {
        return ItemPlaceables.ResolvePlaceable(itemDatabase, brickLookup, itemID);
    }

    /// <inheritdoc/>
    public Result<ItemData> GetLoot(ushort brickDataID)
    {
        Result<Brick> brickResult = brickLookup.Get(brickDataID);
        if (!brickResult.Success)
        {
            return Result<ItemData>.FromFailure((string?)null);
        }

        //  A broken brick grants a stack of that brick's own item.
        return Result<ItemData>.FromSuccess(new ItemData(brickResult.Value.ID, 1, DEFAULT_STACK_SIZE));
    }
}
