using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Swordfish.Graphics;

/// <summary>
///     Represents a 6-plane viewing frustum extracted from a view-projection matrix.
/// </summary>
public readonly struct Frustum
{
    private readonly Vector4 _plane0;
    private readonly Vector4 _plane1;
    private readonly Vector4 _plane2;
    private readonly Vector4 _plane3;
    private readonly Vector4 _plane4;
    private readonly Vector4 _plane5;

    public Frustum(
        in Vector4 plane0,
        in Vector4 plane1,
        in Vector4 plane2,
        in Vector4 plane3,
        in Vector4 plane4,
        in Vector4 plane5
    )
    {
        _plane0 = plane0;
        _plane1 = plane1;
        _plane2 = plane2;
        _plane3 = plane3;
        _plane4 = plane4;
        _plane5 = plane5;
    }

    /// <summary>
    ///     Extracts frustum planes from a view-projection matrix using Gribb-Hartmann.
    /// </summary>
    public static Frustum FromViewProjection(in Matrix4x4 m)
    {
        return new Frustum(
            NormalizePlane(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41),
            NormalizePlane(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41),
            NormalizePlane(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42),
            NormalizePlane(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42),
            NormalizePlane(m.M13, m.M23, m.M33, m.M43),
            NormalizePlane(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 NormalizePlane(float x, float y, float z, float w)
    {
        float length = MathF.Sqrt(x * x + y * y + z * z);
        if (length > 0f)
        {
            float inv = 1f / length;
            return new Vector4(x * inv, y * inv, z * inv, w * inv);
        }

        return Vector4.Zero;
    }

    /// <summary>
    ///     Tests whether an axis-aligned bounding box intersects or lies inside the frustum.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(in Vector3 center, in Vector3 extents)
    {
        return TestPlane(_plane0, center, extents)
            && TestPlane(_plane1, center, extents)
            && TestPlane(_plane2, center, extents)
            && TestPlane(_plane3, center, extents)
            && TestPlane(_plane4, center, extents)
            && TestPlane(_plane5, center, extents);
    }

    /// <summary>
    ///     Tests whether a bounding sphere intersects or lies inside the frustum.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IntersectsSphere(in Vector3 center, float radius)
    {
        return TestPlaneSphere(_plane0, center, radius)
            && TestPlaneSphere(_plane1, center, radius)
            && TestPlaneSphere(_plane2, center, radius)
            && TestPlaneSphere(_plane3, center, radius)
            && TestPlaneSphere(_plane4, center, radius)
            && TestPlaneSphere(_plane5, center, radius);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TestPlane(in Vector4 plane, in Vector3 center, in Vector3 extents)
    {
        float maxDist = plane.X * center.X + plane.Y * center.Y + plane.Z * center.Z
            + MathF.Abs(plane.X) * extents.X
            + MathF.Abs(plane.Y) * extents.Y
            + MathF.Abs(plane.Z) * extents.Z
            + plane.W;

        return maxDist >= 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TestPlaneSphere(in Vector4 plane, in Vector3 center, float radius)
    {
        float dist = plane.X * center.X + plane.Y * center.Y + plane.Z * center.Z + plane.W;
        return dist >= -radius;
    }
}
