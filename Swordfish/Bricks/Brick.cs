using System.Numerics;
using Swordfish.Library.Util;

namespace Swordfish.Bricks;

public struct Brick : IEquatable<Brick>
{
    public const ushort UNDEFINED_ID = 0;

    public static readonly Brick EMPTY = new(UNDEFINED_ID);

    public string Name;
    public readonly ushort ID;
    public Direction Rotation;
    public Direction Orientation;

    public Brick(ushort id)
    {
        ID = id;
        Name = null!;
    }

    public bool Equals(Brick other) => other.ID == ID;

    public override bool Equals(object? obj) => obj is Brick brick && Equals(brick);

    public override int GetHashCode() => HashCode.Combine(ID);

    public static bool operator ==(Brick left, Brick right) => left.Equals(right);

    public static bool operator !=(Brick left, Brick right) => !left.Equals(right);

    public void Build()
    {

    }

    public Vector3 GetDirectionModifier()
    {
        Vector3 modifier = Vector3.One;

        switch (Rotation)
        {
            case Direction.NORTH:
                break;
            case Direction.EAST:
                modifier = new Vector3(-1, 1, -1);
                break;
            case Direction.SOUTH:
                modifier = new Vector3(1, 1, -1);
                break;
            case Direction.WEST:
                modifier = new Vector3(1, 1, -1);
                break;
            case Direction.ABOVE:
                modifier = new Vector3(1, -1, -1);
                break;
            case Direction.BELOW:
                modifier = new Vector3(1, -1, 1);
                break;
        }

        if (ID == 2)
        {
            switch (Orientation)
            {
                case Direction.NORTH:
                    modifier *= new Vector3(-1, -1, 1);
                    break;
                case Direction.EAST:
                    modifier *= new Vector3(1, -1, -1);
                    break;
                case Direction.SOUTH:
                    modifier *= new Vector3(-1, -1, -1);
                    break;
                case Direction.WEST:
                    modifier *= new Vector3(1, -1, -1);
                    break;
                case Direction.ABOVE:
                    modifier *= new Vector3(-1, 1, -1);
                    break;
                case Direction.BELOW:
                    modifier *= new Vector3(-1, 1, 1);
                    break;
            }
        }

        return modifier;
    }

    public Quaternion GetQuaternion()
    {
        Vector3 euler = new(0, 0, 0);

        switch (Rotation)
        {
            case Direction.NORTH:
                break;
            case Direction.EAST:
                euler = new Vector3(0, 90, 0);
                break;
            case Direction.SOUTH:
                euler = new Vector3(0, 180, 0);
                break;
            case Direction.WEST:
                euler = new Vector3(0, 270, 0);
                break;
            case Direction.ABOVE:
                euler = new Vector3(-90, 0, 0);
                break;
            case Direction.BELOW:
                euler = new Vector3(90, 0, 0);
                break;
        }

        switch (Orientation)
        {
            case Direction.NORTH:
                euler = new Vector3(euler.X, euler.Y, euler.Z + 90);
                break;
            case Direction.EAST:
                euler = new Vector3(euler.X, euler.Y + 90, euler.Z + 90);
                break;
            case Direction.SOUTH:
                euler = new Vector3(euler.X, euler.Y + 180, euler.Z - 90);
                break;
            case Direction.WEST:
                euler = new Vector3(euler.X, euler.Y - 90, euler.Z - 90);
                break;
            case Direction.ABOVE:
                euler = new Vector3(euler.X - 90, euler.Y, euler.Z + 90);
                break;
            case Direction.BELOW:
                euler = new Vector3(euler.X + 90, euler.Y + 180, euler.Z - 90);
                break;
        }

        return Quaternion.CreateFromYawPitchRoll(euler.Y * MathS.DegreesToRadians, euler.X * MathS.DegreesToRadians, euler.Z * MathS.DegreesToRadians);
    }
}
