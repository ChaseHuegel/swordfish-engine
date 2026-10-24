using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>A handler's registration key. A null field matches any value.</summary>
public readonly record struct InteractionHandlerFilter(
    InteractionKind? Kind = null,
    string? HeldItemID = null,
    GameMode? GameMode = null
);