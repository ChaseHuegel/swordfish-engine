using System;

namespace WaywardBeyond.Voxels;

/// <summary>Identifies one of the six cardinal brick-local faces.</summary>
public enum BrickFace : byte
{
    Top = 0,
    Bottom = 1,
    Front = 2,
    Back = 3,
    Left = 4,
    Right = 5,
}

/// <summary>Bitmask of brick-local faces.</summary>
[Flags]
public enum BrickFaceMask : byte
{
    None = 0,
    Top = 1 << 0,
    Bottom = 1 << 1,
    Front = 1 << 2,
    Back = 1 << 3,
    Left = 1 << 4,
    Right = 1 << 5,
    All = Top | Bottom | Front | Back | Left | Right,
}