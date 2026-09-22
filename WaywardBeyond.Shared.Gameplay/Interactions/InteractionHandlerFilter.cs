using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// The key an interaction handler registers against to opt into a slice of the interaction space. A null
/// field matches any value: a handler leaves <see cref="Kind"/> null to apply regardless of the button
/// edge, leaves <see cref="HeldItemID"/> null to apply to every held item, and leaves
/// <see cref="GameMode"/> null to apply in every mode. Only handlers whose filter matches a resolved
/// interaction are invoked (the cell context itself is passed to the handler, not used as a key).
/// </summary>
public readonly record struct InteractionHandlerFilter(
    InteractionKind? Kind = null,
    string? HeldItemID = null,
    GameMode? GameMode = null
);