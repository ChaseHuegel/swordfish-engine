namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
///     A server-side hook for interaction outcomes. A handler is registered against an
///     <see cref="InteractionHandlerFilter"/>; it runs after base validation and may return a replacement
///     resolution. Returning a <see cref="InteractionAction.None"/> resolution rejects the interaction.
/// </summary>
public interface IInteractionHandler
{
    /// <summary>The key this handler opts into; null fields match any value.</summary>
    InteractionHandlerFilter Filter { get; }

    /// <summary>Runs after base validation for a matching interaction. Returns the resolution to apply.</summary>
    InteractionResolution Handle(in InteractionContext context);
}