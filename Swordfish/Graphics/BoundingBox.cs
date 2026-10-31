using System;
using System.Numerics;

namespace Swordfish.Graphics;

/// <summary>
///     Represents an axis-aligned bounding box and circumscribed sphere in local space.
/// </summary>
public readonly struct BoundingBox(in Vector3 center, in Vector3 extents, in float sphereRadius) : IEquatable<BoundingBox>
{
    public readonly Vector3 Center = center;
    public readonly Vector3 Extents = extents;
    public readonly float SphereRadius = sphereRadius;

    /// <summary>
    ///     Computes bounds containing all provided vertices.
    /// </summary>
    public static BoundingBox FromVertices(ReadOnlySpan<Vector3> vertices)
    {
        if (vertices.Length == 0)
        {
            return default;
        }

        Vector3 min = vertices[0];
        Vector3 max = vertices[0];

        for (var i = 1; i < vertices.Length; i++)
        {
            min = Vector3.Min(min, vertices[i]);
            max = Vector3.Max(max, vertices[i]);
        }

        Vector3 center = (min + max) * 0.5f;
        Vector3 extents = (max - min) * 0.5f;
        float radiusSq = 0f;

        for (var i = 0; i < vertices.Length; i++)
        {
            float distSq = Vector3.DistanceSquared(center, vertices[i]);
            if (distSq > radiusSq)
            {
                radiusSq = distSq;
            }
        }

        return new BoundingBox(center, extents, MathF.Sqrt(radiusSq));
    }

    /// <summary>
    ///     Transforms the bounds by a matrix and returns the axis-aligned bounds of the result.
    /// </summary>
    public BoundingBox Transform(in Matrix4x4 matrix)
    {
        Vector3 worldCenter = Vector3.Transform(Center, matrix);
        Vector3 worldExtents = new(
            MathF.Abs(matrix.M11) * Extents.X + MathF.Abs(matrix.M21) * Extents.Y + MathF.Abs(matrix.M31) * Extents.Z,
            MathF.Abs(matrix.M12) * Extents.X + MathF.Abs(matrix.M22) * Extents.Y + MathF.Abs(matrix.M32) * Extents.Z,
            MathF.Abs(matrix.M13) * Extents.X + MathF.Abs(matrix.M23) * Extents.Y + MathF.Abs(matrix.M33) * Extents.Z
        );

        return new BoundingBox(worldCenter, worldExtents, worldExtents.Length());
    }

    /// <summary>
    ///     Returns the smallest axis-aligned box that contains both inputs.
    /// </summary>
    public static BoundingBox Union(in BoundingBox left, in BoundingBox right)
    {
        Vector3 min = Vector3.Min(left.Center - left.Extents, right.Center - right.Extents);
        Vector3 max = Vector3.Max(left.Center + left.Extents, right.Center + right.Extents);
        Vector3 extents = (max - min) * 0.5f;
        return new BoundingBox((min + max) * 0.5f, extents, extents.Length());
    }

    /// <inheritdoc/>
    public bool Equals(BoundingBox other)
    {
        return Center.Equals(other.Center) && Extents.Equals(other.Extents) && SphereRadius.Equals(other.SphereRadius);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is BoundingBox other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(Center, Extents, SphereRadius);
    }

    public static bool operator ==(BoundingBox left, BoundingBox right) => left.Equals(right);
    public static bool operator !=(BoundingBox left, BoundingBox right) => !left.Equals(right);
}
