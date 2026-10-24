namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
/// One listener in the interaction pipeline. A handler registers into a single stage and runs for every
/// interaction matching its filter; the stages it does not register into never call it.
/// </summary>
public interface IInteractionHandler
{
    /// <summary>The filter this handler opts into; null fields match any value.</summary>
    InteractionHandlerFilter Filter { get; }

    /// <summary>Processes one matching interaction and decides whether the pipeline continues.</summary>
    InteractionDecision Handle(ref InteractionContext context);
}
