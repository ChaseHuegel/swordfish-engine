#nullable enable
using System;
using System.Numerics;

namespace Swordfish.Library.Types.Shapes;

/// <summary>An immutable 3D convex hull defined by its vertex cloud.</summary>
public sealed class ConvexHull : IEquatable<ConvexHull>
{
    private const float QUANTIZATION_STEP = 0.0001f;

    private readonly Vector3[] _points;
    private readonly int _hashCode;

    /// <summary>The vertices defining this convex hull.</summary>
    public ReadOnlySpan<Vector3> Points => _points;

    /// <summary>The number of vertices in this convex hull.</summary>
    public int Count => _points.Length;

    /// <summary>Creates a convex hull from a copy of the supplied vertices.</summary>
    public ConvexHull(ReadOnlySpan<Vector3> points)
    {
        _points = points.ToArray();

        var hash = new HashCode();
        hash.Add(_points.Length);
        for (var i = 0; i < _points.Length; i++)
        {
            Vector3 point = _points[i];
            hash.Add(Quantize(point.X));
            hash.Add(Quantize(point.Y));
            hash.Add(Quantize(point.Z));
        }

        _hashCode = hash.ToHashCode();
    }

    /// <summary>Implicitly wraps this convex hull in a <see cref="Shape"/>.</summary>
    public static implicit operator Shape(ConvexHull hull) => new(hull);

    /// <inheritdoc/>
    public bool Equals(ConvexHull? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other == null || _hashCode != other._hashCode || _points.Length != other._points.Length)
        {
            return false;
        }

        for (var i = 0; i < _points.Length; i++)
        {
            Vector3 left = _points[i];
            Vector3 right = other._points[i];
            if (Quantize(left.X) != Quantize(right.X) ||
                Quantize(left.Y) != Quantize(right.Y) ||
                Quantize(left.Z) != Quantize(right.Z))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ConvexHull other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _hashCode;

    private static int Quantize(float value) => (int)MathF.Round(value / QUANTIZATION_STEP);
}
