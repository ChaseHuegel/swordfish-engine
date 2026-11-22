using WaywardBeyond.Bricks;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Components;

/// <summary>An outstanding predicted per-face paint edit waiting for reconciliation.</summary>
public readonly struct PendingFaceEdit(
    int entity,
    Int3 coordinate,
    BrickFaceMask faceMask,
    PaintKind mode,
    FaceOverride[] originalOverrides,
    string? predictedPaintId,
    uint sequence,
    uint serverTickAtSample)
{
    public readonly int Entity = entity;
    public readonly Int3 Coordinate = coordinate;
    public readonly BrickFaceMask FaceMask = faceMask;
    public readonly PaintKind Mode = mode;
    public readonly FaceOverride[] OriginalOverrides = originalOverrides;
    public readonly string? PredictedPaintId = predictedPaintId;
    public readonly uint Sequence = sequence;
    public readonly uint ServerTickAtSample = serverTickAtSample;
}