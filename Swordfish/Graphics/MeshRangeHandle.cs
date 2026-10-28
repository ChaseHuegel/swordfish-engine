namespace Swordfish.Graphics;

/// <summary>Identifies a range in a <see cref="MeshBatch"/>.</summary>
public readonly struct MeshRangeHandle(in int slot, in int generation)
{
    public readonly int Slot = slot;
    public readonly int Generation = generation;

    /// <summary>True when the handle was created by an add.</summary>
    public bool IsValid => Slot >= 0;
}