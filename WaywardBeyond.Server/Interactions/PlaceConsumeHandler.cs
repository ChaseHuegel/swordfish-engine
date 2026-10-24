using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;

namespace WaywardBeyond.Server.Interactions;

/// <summary>
/// React stage: consumes one item from the active slot on a survival place. Creative mode is free.
/// </summary>
public sealed class PlaceConsumeHandler : IInteractionHandler
{
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

        int slot = equipment.ActiveInventorySlot;
        if (slot < 0 || slot >= inventory.Contents.Length)
        {
            return InteractionDecision.Continue;
        }

        InventoryComponent updated = inventory;
        updated.Remove(slot, 1);
        context.Store.AddOrUpdate(context.Player, updated);
        return InteractionDecision.Continue;
    }
}
