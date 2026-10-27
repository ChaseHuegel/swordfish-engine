using System;

namespace Reef;

/// <summary>
///     A two-dimensional integer vector.
/// </summary>
public struct IntVector2(int x, int y) : IEquatable<IntVector2>
{
    public static readonly IntVector2 Zero = new(0, 0);

    public int X = x;
    public int Y = y;

    /// <inheritdoc/>
    public readonly bool Equals(IntVector2 other) => X == other.X && Y == other.Y;

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is IntVector2 other && Equals(other);

    /// <inheritdoc/>
    public readonly override int GetHashCode() => HashCode.Combine(X, Y);

    public static bool operator ==(IntVector2 left, IntVector2 right) => left.Equals(right);

    public static bool operator !=(IntVector2 left, IntVector2 right) => !left.Equals(right);

    public static IntVector2 operator +(IntVector2 left, IntVector2 right) => new(left.X + right.X, left.Y + right.Y);

    public static IntVector2 operator -(IntVector2 left, IntVector2 right) => new(left.X - right.X, left.Y - right.Y);
}
