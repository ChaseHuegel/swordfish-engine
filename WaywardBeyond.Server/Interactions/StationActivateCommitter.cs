using Krill;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;

namespace WaywardBeyond.Server.Interactions;

/// <summary>
/// Commit stage, low priority: default handler for <see cref="InteractionKind.Activate"/>. Opens any
/// station brick-entity anchored to the targeted cell, attaching the acting player as a viewer and
/// updating their <see cref="ActiveStationComponent"/>. Higher-priority handlers (such as doors or
/// modded interactables) run first and can claim the activation with <see cref="InteractionDecision.Handled"/>.
/// </summary>
public sealed class StationActivateCommitter : IInteractionHandler
{
    /// <inheritdoc/>
    public InteractionStage Stage => InteractionStage.Commit;

    /// <inheritdoc/>
    public InteractionPriority Priority => InteractionPriority.Low;

    /// <inheritdoc/>
    public InteractionHandlerFilter Filter => new(Kind: InteractionKind.Activate);

    /// <inheritdoc/>
    public InteractionDecision Handle(ref InteractionContext context)
    {
        if (context.Resolution.Action != InteractionAction.Activate || context.Stations == null)
        {
            return InteractionDecision.Continue;
        }

        Uuid structureUuid = context.Store.GetUuid(context.Resolution.Entity);
        if (structureUuid == Uuid.Null)
        {
            return InteractionDecision.Continue;
        }

        Result opened = context.Stations.OpenBrickStation(
            context.Store,
            context.Player,
            context.ClientId,
            structureUuid,
            context.Resolution.Coordinate);

        return opened.Success ? InteractionDecision.Handled : InteractionDecision.Continue;
    }
}
