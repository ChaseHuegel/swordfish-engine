using WaywardBeyond.Bricks;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
/// Resolves the client's hinted cell against the shared interaction rules and records the outcome. The
/// default resolver: a custom resolver registered at a higher priority pre-empts it, and a hint it cannot
/// resolve leaves the stage open for a later handler.
/// </summary>
public sealed class HintResolveHandler(IBrickRegistry brickRegistry) : IInteractionHandler
{
    /// <inheritdoc/>
    public InteractionHandlerFilter Filter => default;

    /// <inheritdoc/>
    public InteractionDecision Handle(ref InteractionContext context)
    {
        InteractionRequest request = context.BuildRequest();
        context.Resolution = InteractionResolver.Resolve(in request, context.World, brickRegistry);

        //  A valid outcome ends the stage. An unresolved hint stays open so a later resolver can claim it.
        return context.Resolution.Action != InteractionAction.None
            ? InteractionDecision.Handled
            : InteractionDecision.Continue;
    }
}
