using System.Numerics;
using Swordfish.Library.Util;

namespace Swordfish.Bricks;

public struct Brick(in ushort id) : IEquatable<Brick>
{
    // ReSharper disable once MemberCanBePrivate.Global
    public const ushort UNDEFINED_ID = 0;

    public static readonly Brick Empty = new(UNDEFINED_ID);

    public string? Name = null;
    public readonly ushort ID = id;
    public Direction Rotation;
    public Direction Orientation;

    public bool Equals(Brick other) => other.ID == ID;

    public override bool Equals(object? obj) => obj is Brick brick && Equals(brick);

    public override int GetHashCode() => HashCode.Combine(ID);

    public static bool operator ==(Brick left, Brick right) => left.Equals(right);

    public static bool operator !=(Brick left, Brick right) => !left.Equals(right);

    public void Build()
    {
        //  TODO implement
    }

    // ReSharper disable once UnusedMember.Global
    public Vector3 GetDirectionModifier()
    {
        Vector3 modifier = Vector3.One;

        switch (Rotation)
        {
            case Direction.North:
                break;
            case Direction.East:
                modifier = new Vector3(-1, 1, -1);
                break;
            case Direction.South:
                modifier = new Vector3(1, 1, -1);
                break;
            case Direction.West:
                modifier = new Vector3(1, 1, -1);
                break;
            case Direction.Above:
                modifier = new Vector3(1, -1, -1);
                break;
            case Direction.Below:
                modifier = new Vector3(1, -1, 1);
                break;
        }

        if (ID != 2)    //  TODO hacky harcoded value for the demo
        {
            return modifier;
        }

        switch (Orientation)
        {
            case Direction.North:
                modifier *= new Vector3(-1, -1, 1);
                break;
            case Direction.East:
                modifier *= new Vector3(1, -1, -1);
                break;
            case Direction.South:
                modifier *= new Vector3(-1, -1, -1);
                break;
            case Direction.West:
                modifier *= new Vector3(1, -1, -1);
                break;
            case Direction.Above:
                modifier *= new Vector3(-1, 1, -1);
                break;
            case Direction.Below:
                modifier *= new Vector3(-1, 1, 1);
                break;
        }

        return modifier;
    }

    public Quaternion GetQuaternion()
    {
        Vector3 euler = new(0, 0, 0);

        switch (Rotation)
        {
            case Direction.North:
                break;
            case Direction.East:
                euler = new Vector3(0, 90, 0);
                break;
            case Direction.South:
                euler = new Vector3(0, 180, 0);
                break;
            case Direction.West:
                euler = new Vector3(0, 270, 0);
                break;
            case Direction.Above:
                euler = new Vector3(-90, 0, 0);
                break;
            case Direction.Below:
                euler = new Vector3(90, 0, 0);
                break;
        }

        euler = Orientation switch
        {
            Direction.North => euler with { Z = euler.Z + 90 },
            Direction.East => new Vector3(euler.X, euler.Y + 90, euler.Z + 90),
            Direction.South => new Vector3(euler.X, euler.Y + 180, euler.Z - 90),
            Direction.West => new Vector3(euler.X, euler.Y - 90, euler.Z - 90),
            Direction.Above => new Vector3(euler.X - 90, euler.Y, euler.Z + 90),
            Direction.Below => new Vector3(euler.X + 90, euler.Y + 180, euler.Z - 90),
            _ => euler,
        };

        return Quaternion.CreateFromYawPitchRoll(euler.Y * MathS.DEGREES_TO_RADIANS, euler.X * MathS.DEGREES_TO_RADIANS, euler.Z * MathS.DEGREES_TO_RADIANS);
    }
}
