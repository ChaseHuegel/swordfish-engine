using System;

namespace WaywardBeyond.Voxels;

/// <summary>Bitmask of axis reflections applied to a voxel region.</summary>
[Flags]
public enum MirrorAxes : byte
{
    None = 0,
    X = 1 << 0,
    Y = 1 << 1,
    Z = 1 << 2,
}
