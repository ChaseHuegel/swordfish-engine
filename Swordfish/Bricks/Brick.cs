using System.Numerics;

namespace Swordfish.Bricks;

public struct Brick : IEquatable<Brick>
{
    // ReSharper disable once MemberCanBePrivate.Global
    public const ushort UNDEFINED_ID = 0;

    public static readonly Brick Empty = new(UNDEFINED_ID);

    public readonly ushort ID;
    
    public byte Data;
    public BrickOrientation Orientation;

    public Brick(in ushort id)
    {
        ID = id;
    }
    
    public Brick(in ushort id, in byte data)
    {
        ID = id;
        Data = data;
    }

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
        return $"{ID}:{Data} / {Orientation}";
    }
}
