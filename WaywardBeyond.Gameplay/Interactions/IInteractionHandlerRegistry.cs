namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>Server-side registry of interaction handlers.</summary>
public interface IInteractionHandlerRegistry
{
    /// <summary>Registers a handler. It runs for every interaction matching its filter.</summary>
    void Register(IInteractionHandler handler);

    /// <summary>
    /// Runs every matching handler in registration order. Returning a <see cref="InteractionAction.None"/>
    /// resolution rejects the interaction and short-circuits; otherwise the last matching resolution wins.
    /// </summary>
    InteractionResolution Apply(in InteractionRequest request, in InteractionResolution baseResolution, string? heldItemID);
}