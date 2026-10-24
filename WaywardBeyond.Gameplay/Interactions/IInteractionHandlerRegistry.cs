namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>The registry of interaction handlers the authoritative resolver runs.</summary>
public interface IInteractionHandlerRegistry
{
    /// <summary>Registers a handler. It runs for every interaction matching its filter.</summary>
    void Register(IInteractionHandler handler);

    /// <summary>
    /// Runs every matching handler in registration order. Each handler receives the running resolution,
    /// so an override carries forward. Returning a <see cref="InteractionAction.None"/> resolution
    /// rejects the interaction and short-circuits; otherwise the last matching resolution wins.
    /// </summary>
    InteractionResolution Apply(in InteractionRequest request, in InteractionResolution baseResolution, string? heldItemID);
}