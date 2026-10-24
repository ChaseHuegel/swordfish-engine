using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Items;

namespace WaywardBeyond.Server.Interactions;

/// <summary>
/// Select stage: resolves the held item to the brick it places. An item that places nothing leaves the
/// stage open, so a break still resolves and a custom content source can claim the item first.
/// </summary>
public sealed class ItemPlaceableHandler(IItemDatabase items, IBrickDatabase bricks) : IInteractionHandler
{
    /// <inheritdoc/>
    public InteractionHandlerFilter Filter => default;

    /// <inheritdoc/>
    public InteractionDecision Handle(ref InteractionContext context)
    {
        if (context.HeldItemID == null)
        {
            return InteractionDecision.Continue;
        }

        Result<Brick> placeable = ItemPlaceables.ResolvePlaceable(items, bricks, context.HeldItemID);
        if (!placeable.Success)
        {
            return InteractionDecision.Continue;
        }

        context.Placeable = placeable.Value;
        return InteractionDecision.Handled;
    }
}
