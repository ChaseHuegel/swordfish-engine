using System.Numerics;

namespace Swordfish.Library.Types.Shapes
{
    public struct Box2 : IShape
    {
        public Vector2 Extents;

        public Box2(Vector2 extents)
        {
            Extents = extents;
        }
    }
}