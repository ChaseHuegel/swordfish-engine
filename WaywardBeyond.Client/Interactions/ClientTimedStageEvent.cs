using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Client.Interactions;

/// <summary>Discrete interaction edge emitted by <see cref="ClientTimedInteractionController"/> when a hold starts, retargets, or cancels.</summary>
internal readonly struct ClientTimedStageEvent(
    bool hasEvent,
    InteractionKind kind,
    BrickInteraction? brickHint = null,
    PaintInteraction? paintHint = null
) {
    /// <summary>No event to stage on this tick.</summary>
    public static ClientTimedStageEvent None => default;

    /// <summary>Whether an interaction event must be staged.</summary>
    public readonly bool HasEvent = hasEvent;

    /// <summary>The interaction edge to stage.</summary>
    public readonly InteractionKind Kind = kind;

    /// <summary>Optional voxel target hint.</summary>
    public readonly BrickInteraction? BrickHint = brickHint;

    /// <summary>Optional paint target hint.</summary>
    public readonly PaintInteraction? PaintHint = paintHint;
}
