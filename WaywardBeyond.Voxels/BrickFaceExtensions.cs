using System;
using System.Numerics;

namespace WaywardBeyond.Voxels;

/// <summary>Helpers for converting between <see cref="BrickFace"/>, <see cref="BrickFaceMask"/>, and normals.</summary>
public static class BrickFaceExtensions
{
    /// <summary>Converts a single face to its bitmask flag.</summary>
    public static BrickFaceMask ToMask(this BrickFace face) => (BrickFaceMask)(1 << (int)face);

    /// <summary>Returns true when <paramref name="mask"/> includes <paramref name="face"/>.</summary>
    public static bool HasFace(this BrickFaceMask mask, BrickFace face) => (mask & face.ToMask()) != 0;

    /// <summary>Returns the unit brick-local normal vector for <paramref name="face"/>.</summary>
    public static Vector3 ToLocalNormal(this BrickFace face)
    {
        return face switch
        {
            BrickFace.Top => Vector3.UnitY,
            BrickFace.Bottom => -Vector3.UnitY,
            BrickFace.Front => Vector3.UnitZ,
            BrickFace.Back => -Vector3.UnitZ,
            BrickFace.Left => -Vector3.UnitX,
            BrickFace.Right => Vector3.UnitX,
            _ => Vector3.UnitY,
        };
    }

    /// <summary>Classifies a brick-local normal to its dominant cardinal <see cref="BrickFace"/> (tie-break Z, Y, X).</summary>
    public static BrickFace FromLocalNormal(Vector3 localNormal)
    {
        float ax = MathF.Abs(localNormal.X);
        float ay = MathF.Abs(localNormal.Y);
        float az = MathF.Abs(localNormal.Z);

        //  Match MeshPostPass tie-break priority (Z, Y, X) so 45-degree slopes resolve to Front.
        if (az >= ay - 1e-4f && az >= ax - 1e-4f)
        {
            return localNormal.Z >= 0f ? BrickFace.Front : BrickFace.Back;
        }

        if (ay >= ax - 1e-4f)
        {
            return localNormal.Y >= 0f ? BrickFace.Top : BrickFace.Bottom;
        }

        return localNormal.X >= 0f ? BrickFace.Right : BrickFace.Left;
    }
}