using Swordfish.Physics;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// The shared, single-location input the interaction resolver decides over. Both client prediction and
/// server authority construct this identically: the authority/predicted aim ray, the target cell hint the
/// client resolved (an <see cref="InteractionHint"/>-free event is first-class and resolves to
/// <see cref="InteractionAction.None"/>), the button edge, the resolved held placeable (null when the held
/// item isn't a placeable brick), the game mode, and the interaction reach.
/// </summary>
public readonly struct InteractionRequest
{
    /// <summary>World-space aim ray (origin confirms reach; the caller-built vector carries the aim).</summary>
    public readonly Ray Ray;

    /// <summary>The client's resolved target cell hint; hints are matched against an independent resolution.</summary>
    public readonly BrickInteraction? Hint;

    /// <summary>The button edge that produced this interaction; primary = break, secondary = place.</summary>
    public readonly InteractionKind Kind;

    /// <summary>The resolved placeable brick for the held item, when it places one.</summary>
    public readonly PlaceableBrick? Placeable;

    public readonly GameMode GameMode;

    /// <summary>Maximum distance the interaction may reach, from the ray origin.</summary>
    public readonly float Reach;

    public InteractionRequest(
        in Ray ray,
        BrickInteraction? hint,
        InteractionKind kind,
        PlaceableBrick? placeable,
        GameMode gameMode,
        float reach
    ) {
        Ray = ray;
        Hint = hint;
        Kind = kind;
        Placeable = placeable;
        GameMode = gameMode;
        Reach = reach;
    }
}