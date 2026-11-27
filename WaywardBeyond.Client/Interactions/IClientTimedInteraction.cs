using WaywardBeyond.Gameplay.Interactions;

namespace WaywardBeyond.Client.Interactions;

/// <summary>
/// Extension point for client-side timed hold interactions (such as Adventure Mode brick breaking).
/// Register implementations via DI to claim a held interaction input, compute expected duration, and stage
/// start/cancel interaction edges without editing <see cref="Systems.PlayerInteractionService"/>.
/// </summary>
public interface IClientTimedInteraction
{
    /// <summary>The logical interaction input channel this timed interaction claims when active.</summary>
    InteractionInput Input { get; }

    /// <summary>Returns whether this timed interaction applies to the player's current mode and equipment.</summary>
    bool AppliesTo(in ClientTimedInteractionContext context);

    /// <summary>Attempts to resolve the current timed target and duration for this tick.</summary>
    bool TryResolveTarget(in ClientTimedInteractionContext context, out ClientTimedTarget target);
}
