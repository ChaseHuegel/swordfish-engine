namespace Reef;

public readonly struct IntVector2(int x, int y)
{
    public readonly int X = x;
    public readonly int Y = y;

    public IntVector2(int value) : this(value, value) { }
}