using Swordfish.Library.Util;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Items;

namespace WaywardBeyond.Server.Interactions;

/// <summary>
/// Validate stage: a break needs a removal tool in hand. A break resolves from the primary edge without a
/// held placeable, and this handler rejects it before the write when the active slot is not a removal tool.
/// </summary>
public sealed class BreakToolValidator(IItemDatabase items) : IInteractionHandler
{
    /// <inheritdoc/>
    public InteractionStage Stage => InteractionStage.Validate;

    /// <inheritdoc/>
    public InteractionPriority Priority => InteractionPriority.Normal;

    /// <inheritdoc/>
    public InteractionHandlerFilter Filter => default;

    /// <inheritdoc/>
    public InteractionDecision Handle(ref InteractionContext context)
    {
        if (context.Resolution.Action != InteractionAction.Break)
        {
            return InteractionDecision.Continue;
        }

        if (string.IsNullOrEmpty(context.HeldItemID))
        {
            return InteractionDecision.Cancel;
        }

        Result<Item> itemResult = items.Get(context.HeldItemID);
        if (!itemResult.Success || itemResult.Value.Tool?.Type != ToolType.Mining)
        {
            return InteractionDecision.Cancel;
        }

        return InteractionDecision.Continue;
    }
}
