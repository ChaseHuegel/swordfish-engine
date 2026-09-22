namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// A server-side mod hook for interaction outcomes. A mod registers handlers against an
/// <see cref="InteractionHandlerFilter"/> key; for every resolved interaction whose filter matches, the
/// handler runs after base validation and returns the authoritative resolution to apply:
/// <list type="bullet">
/// <item>return <see cref="InteractionResolution.None"/> to **reject** the interaction (the shared
/// default will not be applied);</item>
/// <item>return the unchanged <see cref="InteractionContext.Resolution"/> to allow it;</item>
/// <item>return a different <see cref="InteractionResolution"/> to **override/augment** the base outcome
/// (e.g. a different target cell, voxel, or even a different action).</item>
/// </list>
/// Handlers are entirely server-side - no client mod is involved.
/// </summary>
public interface IInteractionHandler
{
    /// <summary>The key this handler opts into; null fields match any value.</summary>
    InteractionHandlerFilter Filter { get; }

    /// <summary>
    /// Runs after base validation for a matching interaction. Returns the resolution to apply; returning
    /// <see cref="InteractionResolution.None"/> rejects the interaction.
    /// </summary>
    InteractionResolution Handle(in InteractionContext context);
}