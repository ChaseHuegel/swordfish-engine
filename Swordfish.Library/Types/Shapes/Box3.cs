using System.Numerics;

namespace Swordfish.Library.Types.Shapes
{
    public struct Box3
    {
        public Vector3 Extents;

        public Box3(Vector3 extents)
        {
            Extents = extents;
        }

        public static implicit operator Shape(Box3 x) => new(x);
    }
}