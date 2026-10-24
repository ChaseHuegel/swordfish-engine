using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;

namespace WaywardBeyond.Server.Interactions;

/// <summary>
/// React stage: grants the broken brick's own item in survival. Creative mode is free.
/// </summary>
public sealed class BreakLootReactor(IBrickDatabase brickDatabase) : IInteractionHandler
{
    private const int DEFAULT_STACK_SIZE = 100;

    /// <inheritdoc/>
    public InteractionStage Stage => InteractionStage.React;

    /// <inheritdoc/>
    public InteractionPriority Priority => InteractionPriority.Normal;

    /// <inheritdoc/>
    public InteractionHandlerFilter Filter => default;

    /// <inheritdoc/>
    public InteractionDecision Handle(ref InteractionContext context)
    {
        if (context.Resolution.Action != InteractionAction.Break || context.GameMode == GameMode.Creative)
        {
            return InteractionDecision.Continue;
        }

        Result<Brick> broken = brickDatabase.Get(context.Resolution.Voxel.ID);
        if (!broken.Success)
        {
            return InteractionDecision.Continue;
        }

        if (!context.Store.TryGet(context.Player, out InventoryComponent inventory))
        {
            return InteractionDecision.Continue;
        }

        InventoryComponent updated = inventory;
        updated.Add(new ItemData(broken.Value.ID, 1, DEFAULT_STACK_SIZE));
        context.Store.AddOrUpdate(context.Player, updated);
        return InteractionDecision.Continue;
    }
}
