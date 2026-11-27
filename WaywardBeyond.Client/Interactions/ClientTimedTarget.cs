using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;

namespace WaywardBeyond.Client.Interactions;

/// <summary>Resolved target and timing requirement produced by a <see cref="IClientTimedInteraction"/>.</summary>
public readonly struct ClientTimedTarget(
    TimedInteractionKey key,
    float requiredDuration,
    InteractionInput input = InteractionInput.Primary,
    BrickInteraction? brickHint = null,
    PaintInteraction? paintHint = null
) {
    /// <summary>Stable identity of the held target; changing it restarts local progress and re-stages the start event.</summary>
    public readonly TimedInteractionKey Key = key;

    /// <summary>Expected hold duration in seconds.</summary>
    public readonly float RequiredDuration = float.IsFinite(requiredDuration) && requiredDuration >= 0f ? requiredDuration : 0f;

    /// <summary>Logical interaction input channel driving start and cancel wire edges.</summary>
    public readonly InteractionInput Input = input;

    /// <summary>Optional voxel target hint carried by the start event.</summary>
    public readonly BrickInteraction? BrickHint = brickHint;

    /// <summary>Optional paint target hint carried by the start event.</summary>
    public readonly PaintInteraction? PaintHint = paintHint;

    /// <summary>Interaction edge staged when starting or retargeting the hold.</summary>
    public InteractionKind StartKind => Input.ToPressedKind();

    /// <summary>Interaction edge staged when cancelling or releasing the hold.</summary>
    public InteractionKind CancelKind => Input.ToReleasedKind();
}
