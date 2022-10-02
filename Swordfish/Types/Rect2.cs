using System.Numerics;

namespace Swordfish.Types;

public struct Rect2
{
    public Vector2 Min;

    public Vector2 Max;

    public Rect2(Vector2 min, Vector2 max)
    {
        Min = min;
        Max = max;
    }
}
