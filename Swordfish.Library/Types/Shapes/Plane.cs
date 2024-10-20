using System.Numerics;

namespace Swordfish.Library.Types.Shapes
{
    public struct Plane : IShape
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
    }
}