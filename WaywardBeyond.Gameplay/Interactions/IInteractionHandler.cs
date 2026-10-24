namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
/// One listener in the interaction pipeline. A handler declares the stage it belongs to and its priority
/// inside that stage, so the type carries its own placement; the registry discovers handlers through DI.
/// </summary>
public interface IInteractionHandler
{
    /// <summary>The stage this handler belongs to.</summary>
    InteractionStage Stage { get; }

    /// <summary>The priority band inside <see cref="Stage"/>.</summary>
    InteractionPriority Priority { get; }

    /// <summary>The filter this handler opts into; null fields match any value.</summary>
    InteractionHandlerFilter Filter { get; }

    /// <summary>Processes one matching interaction and decides whether the pipeline continues.</summary>
    InteractionDecision Handle(ref InteractionContext context);
}
