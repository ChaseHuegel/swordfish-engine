namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
/// The ordered interaction handlers. Handlers register into one stage at a priority; within a stage they
/// run by priority and then registration order. The composition of handlers decides the outcome, so the
/// game registers the same kinds of handlers a mod would.
/// </summary>
public interface IInteractionHandlerRegistry
{
    /// <summary>Registers a handler in a stage. Handlers run by priority, then registration order.</summary>
    void Register(InteractionStage stage, InteractionPriority priority, IInteractionHandler handler);

    /// <summary>Runs every stage for one interaction, stopping on <c>Consume</c> or <c>Cancel</c>.</summary>
    InteractionRunResult Run(ref InteractionContext context);
}
