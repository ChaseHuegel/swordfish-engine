using System.Collections.Generic;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
/// The interaction pipeline built from every registered <see cref="IInteractionHandler"/>. Handlers are
/// placed by their own stage and priority, so the composition only decides which handlers exist. The
/// composition of handlers decides the outcome, so the game registers the same kinds of handlers a mod
/// would.
/// </summary>
public interface IInteractionHandlerRegistry
{
    /// <summary>Runs every stage for one interaction, stopping on <c>Consume</c> or <c>Cancel</c>.</summary>
    InteractionRunResult Run(ref InteractionContext context);

    /// <summary>The handlers in one stage, ordered by priority and then DI registration order.</summary>
    IReadOnlyList<IInteractionHandler> GetHandlers(InteractionStage stage);
}
