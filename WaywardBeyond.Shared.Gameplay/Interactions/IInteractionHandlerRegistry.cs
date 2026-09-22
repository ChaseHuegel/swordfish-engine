namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// Server-side registry of interaction mod handlers. Mods register via <see cref="Register"/> at load; the
/// authoritative <c>ServerInteractionSystem</c> resolves every interaction through <see cref="Apply"/>.
/// Registered as a singleton so any number of server mods can add handlers against the same instance.
/// </summary>
public interface IInteractionHandlerRegistry
{
    /// <summary>Registers a handler; it runs for every interaction matching its filter.</summary>
    void Register(IInteractionHandler handler);

    /// <summary>
    /// Runs every handler whose filter matches the resolved interaction, in registration order. Each
    /// handler returns the resolution to apply; returning <see cref="InteractionResolution.None"/> rejects
    /// the interaction and short-circuits. Otherwise the last matching handler's resolution wins
    /// (override/augment). Returns the resolution the server should apply.
    /// </summary>
    InteractionResolution Apply(in InteractionRequest request, in InteractionResolution baseResolution, string? heldItemID);
}