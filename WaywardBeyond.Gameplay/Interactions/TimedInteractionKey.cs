using Krill;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
/// Stable identity of one active timed interaction target. Changing any part of the key restarts
/// timed progress from zero.
/// </summary>
public readonly record struct TimedInteractionKey(
    string HandlerId,
    Uuid TargetEntity,
    Int3 TargetCoord,
    uint TargetData = 0
);
