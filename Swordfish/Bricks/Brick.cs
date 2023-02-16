namespace Swordfish.Bricks;

public struct Brick : IEquatable<Brick>
{
    public const ushort UNDEFINED_ID = 0;

    public static readonly Brick EMPTY = new(UNDEFINED_ID);

    public readonly ushort ID;

    public Brick(ushort id)
    {
        ID = id;
    }

    public bool Equals(Brick other) => other.ID == ID;

    public override bool Equals(object? obj) => obj is Brick brick && Equals(brick);

    public override int GetHashCode() => HashCode.Combine(ID);

    public static bool operator ==(Brick left, Brick right) => left.Equals(right);

    public static bool operator !=(Brick left, Brick right) => !left.Equals(right);

    public void Build()
    {

    }
}
