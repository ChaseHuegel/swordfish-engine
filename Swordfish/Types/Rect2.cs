using System.Numerics;

namespace Swordfish.Types;

public record struct Rect2
{
    public Vector2 Min;

    public Vector2 Max;

    public Rect2(Vector2 min, Vector2 max)
    {
        Min = min;
        Max = max;
    }

    public override string ToString()
    {
        return $"{Min},{Max}";
    }
}
