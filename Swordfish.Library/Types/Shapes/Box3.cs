using System.Numerics;

namespace Swordfish.Library.Types.Shapes
{
    public struct Box3 : IShape
    {
        public Vector3 Extents;

        public Box3(Vector3 extents)
        {
            Extents = extents;
        }
    }
}