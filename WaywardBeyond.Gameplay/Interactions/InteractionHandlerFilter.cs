using WaywardBeyond.Data;
using WaywardBeyond.Networking.Components;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>A handler's registration key; a null field matches any value.</summary>
public readonly record struct InteractionHandlerFilter(
    InteractionKind? Kind = null,
    string? HeldItemID = null,
    GameMode? GameMode = null
);