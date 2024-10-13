using System.Numerics;

namespace Swordfish.Physics
{
    public readonly struct Ray(Vector3 origin, Vector3 vector)
    {
        public Vector3 Origin { get; } = origin;
        public Vector3 Vector { get; } = vector;

        public static Ray operator *(Ray left, float right) => new(left.Origin, left.Vector * right);
        public static Ray operator *(Ray left, int right) => new(left.Origin, left.Vector * right);
    }
}