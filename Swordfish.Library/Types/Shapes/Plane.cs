using System.Numerics;

namespace Swordfish.Library.Types.Shapes
{
    public struct Plane : IShape
    {
        public Vector3 Normal;
        public float Extent;

        public Plane(Vector3 normal, float extent)
        {
            Normal = normal;
            Extent = extent;
        }
    }
}