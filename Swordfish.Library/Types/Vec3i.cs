using System;

namespace Swordfish.Library.Types
{
    public struct Vec3i : IEquatable<Vec3i>
    {
        public readonly static Vec3i Zero = new Vec3i(0);
        public readonly static Vec3i One = new Vec3i(1);

        public int X;
        public int Y;
        public int Z;

        public Vec3i(int value)
        {
            X = value;
            Y = value;
            Z = value;
        }

        public Vec3i(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return $"{X}, {Y}, {Z}";
        }

        public bool Equals(Vec3i other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object obj) => obj is Vec3i brick && Equals(brick);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 133774;
                hashCode = hashCode * 711 ^ X.GetHashCode();
                hashCode = hashCode * 711 ^ Y.GetHashCode();
                hashCode = hashCode * 711 ^ Z.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(Vec3i left, Vec3i right) => left.Equals(right);

        public static bool operator !=(Vec3i left, Vec3i right) => !left.Equals(right);

        public static Vec3i operator +(Vec3i value) => value;

        public static Vec3i operator -(Vec3i value) => new Vec3i(-value.X, -value.Y, -value.Z);

        public static Vec3i operator +(Vec3i left, Vec3i right) => new Vec3i(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

        public static Vec3i operator -(Vec3i left, Vec3i right) => new Vec3i(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

        public static Vec3i operator *(Vec3i left, Vec3i right) => new Vec3i(left.X * right.X, left.Y * right.Y, left.Z * right.Z);

        public static Vec3i operator /(Vec3i left, Vec3i right) => new Vec3i(left.X / right.X, left.Y / right.Y, left.Z / right.Z);

        public static Vec3i operator *(Vec3i left, int right) => new Vec3i(left.X * right, left.Y * right, left.Z * right);

        public static Vec3i operator /(Vec3i left, int right) => new Vec3i(left.X / right, left.Y / right, left.Z / right);
    }
}
