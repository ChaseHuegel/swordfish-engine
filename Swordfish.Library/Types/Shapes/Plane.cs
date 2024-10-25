using System.Numerics;

namespace Swordfish.Library.Types.Shapes
{
    public struct Plane
    {
        public Vector3 Normal;
        public float Distance;

        public Plane(Vector3 normal, float distance = 0f)
        {
            Normal = normal;
            Distance = distance;
        }

        public Vector3 GetPosition()
        {
            return Normal * Distance;
        }

        public static implicit operator Shape(Plane x) => new(x);
    }
}