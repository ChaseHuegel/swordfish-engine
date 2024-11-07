using System.Numerics;

namespace Swordfish.Library.Types.Shapes;

public struct Box3(in Vector3 extents)
{
    public Vector3 Extents = extents;

    public static implicit operator Shape(Box3 x) => new(x);
}