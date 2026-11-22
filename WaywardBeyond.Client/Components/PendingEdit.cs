using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Components;

/// <summary>An outstanding predicted voxel edit waiting for reconciliation.</summary>
public readonly struct PendingEdit(
    int entity,
    Int3 coordinate,
    Voxel original,
    Voxel predicted,
    uint sequence,
    uint serverTickAtSample)
{
    public readonly int Entity = entity;
    public readonly Int3 Coordinate = coordinate;
    public readonly Voxel Original = original;
    public readonly Voxel Predicted = predicted;
    public readonly uint Sequence = sequence;
    public readonly uint ServerTickAtSample = serverTickAtSample;
}