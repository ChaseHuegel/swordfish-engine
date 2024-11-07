using System.Numerics;

namespace Swordfish.Types;

public struct Rect2(in Vector2 min, in Vector2 max)
{
    public Vector2 Min = min;

    public Vector2 Max = max;
}
