using System.Numerics;

namespace Swordfish.Bricks;

public struct Brick(in ushort id) : IEquatable<Brick>
{
    // ReSharper disable once MemberCanBePrivate.Global
    public const ushort UNDEFINED_ID = 0;

    public static readonly Brick Empty = new(UNDEFINED_ID);

    public string? Name = null;
    public readonly ushort ID = id;
    public BrickOrientation Orientation;

    public bool Equals(Brick other) => other.ID == ID;

    public override bool Equals(object? obj) => obj is Brick brick && Equals(brick);

    public override int GetHashCode() => ID;

    public static bool operator ==(Brick left, Brick right) => left.Equals(right);

    public static bool operator !=(Brick left, Brick right) => !left.Equals(right);

    public Quaternion GetQuaternion()
    {
        return Orientation.ToQuaternion();
    }

    public override string ToString()
    {
        return $"{ID}:{Name ?? "UNDEFINED"} / {Orientation}";
    }
}
