using System.Numerics;

namespace Swordfish.Library.Types.Shapes;

public struct Box2(in Vector2 extents)
{
    public Vector2 Extents = extents;

    public static implicit operator Shape(Box2 x) => new(x);
}