using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Items;

namespace WaywardBeyond.Server.Interactions;

/// <summary>
/// Resolves paint interactions carrying a <see cref="InteractionContext.PaintHint"/> before the default brick hint resolver,
/// authorizing the held tool against the requested paint mode.
/// </summary>
public sealed class PaintHintResolver(
    IBrickDatabase brickDatabase,
    ItemDatabase? itemDatabase = null,
    IPaintDatabase? paintDatabase = null,
    IPaintRegistry? paintRegistry = null
) : IInteractionHandler
{
    /// <inheritdoc/>
    public InteractionStage Stage => InteractionStage.Resolve;

    /// <inheritdoc/>
    public InteractionPriority Priority => InteractionPriority.High;

    /// <inheritdoc/>
    public InteractionHandlerFilter Filter => default;

    /// <inheritdoc/>
    public InteractionDecision Handle(ref InteractionContext context)
    {
        if (context.PaintHint == null)
        {
            return InteractionDecision.Continue;
        }

        if (paintDatabase == null || paintRegistry == null)
        {
            return InteractionDecision.Cancel;
        }

        if (itemDatabase != null && !IsHeldToolAuthorized(context.HeldItemID, context.PaintHint.Value.Mode))
        {
            return InteractionDecision.Cancel;
        }

        InteractionRequest request = context.BuildRequest();
        context.Resolution = PaintInteractionResolver.Resolve(in request, context.World, brickDatabase, paintDatabase, paintRegistry);
        return context.Resolution.Action == InteractionAction.Paint
            ? InteractionDecision.Handled
            : InteractionDecision.Cancel;
    }

    private bool IsHeldToolAuthorized(string? heldItemId, byte paintMode)
    {
        if (string.IsNullOrEmpty(heldItemId))
        {
            return false;
        }

        Result<Item> itemResult = itemDatabase!.Get(heldItemId);
        if (!itemResult.Success || itemResult.Value.Tool == null)
        {
            return false;
        }

        ToolType toolType = itemResult.Value.Tool.Value.Type;
        return (PaintKind)paintMode switch
        {
            PaintKind.Color => toolType == ToolType.Sprayer,
            PaintKind.Texture => toolType == ToolType.Drill,
            _ => false,
        };
    }
}
