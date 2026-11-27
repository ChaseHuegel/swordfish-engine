using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;

namespace WaywardBeyond.Server.Interactions;

/// <summary>
/// React stage: consumes one item from the active slot on a survival place. Creative mode is free.
/// </summary>
public sealed class PlaceConsumeReactor : IInteractionHandler
{
    /// <inheritdoc/>
    public InteractionStage Stage => InteractionStage.React;

    /// <inheritdoc/>
    public InteractionPriority Priority => InteractionPriority.Normal;

    /// <inheritdoc/>
    public InteractionHandlerFilter Filter => default;

    /// <inheritdoc/>
    public InteractionDecision Handle(ref InteractionContext context)
    {
        if (context.Resolution.Action != InteractionAction.Place || context.GameMode == GameMode.Creative)
        {
            return InteractionDecision.Continue;
        }

        if (!context.Store.TryGet(context.Player, out EquipmentComponent equipment) ||
            !context.Store.TryGet(context.Player, out InventoryComponent inventory))
        {
            return InteractionDecision.Continue;
        }

        var hotbarSlot = new SlotId(SlotGroups.HOTBAR, equipment.ActiveInventorySlot);
        if (inventory.Get(in hotbarSlot).IsEmpty())
        {
            return InteractionDecision.Continue;
        }

        InventoryComponent updated = inventory.Clone();
        updated.Remove(in hotbarSlot, 1);
        context.Store.AddOrUpdate(context.Player, updated);
        return InteractionDecision.Continue;
    }
}
