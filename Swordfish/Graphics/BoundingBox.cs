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
