using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Networking.Components;

namespace WaywardBeyond.Server;

/// <inheritdoc/>
public sealed class ServerInteractionContent(IBrickDatabase brickLookup) : IInteractionContent
{
    private const int DEFAULT_STACK_SIZE = 100;

    /// <inheritdoc/>
    public Result<Brick> GetPlaceable(string? itemID)
    {
        //  No shared item database: the dedicated server cannot map an item id to a brick.
        return Result<Brick>.FromFailure((string?)null);
    }

    /// <inheritdoc/>
    public Result<ItemData> GetLoot(ushort brickDataID)
    {
        Result<Brick> brickResult = brickLookup.Get(brickDataID);
        if (!brickResult.Success)
        {
            return Result<ItemData>.FromFailure((string?)null);
        }

        //  A broken brick grants a stack of that brick's own item, like the client-hosted handler.
        return Result<ItemData>.FromSuccess(new ItemData(brickResult.Value.ID, 1, DEFAULT_STACK_SIZE));
    }
}