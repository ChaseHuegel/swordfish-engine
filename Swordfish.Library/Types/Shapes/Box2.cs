using System.Numerics;

namespace Swordfish.Library.Types.Shapes
{
    public struct Box2
    {
        public Vector2 Extents;

        public Box2(Vector2 extents)
        {
            Extents = extents;
        }

        public static implicit operator Shape(Box2 x) => new(x);
    }
}