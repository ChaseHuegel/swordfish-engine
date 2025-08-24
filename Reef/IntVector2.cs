namespace Reef;

public struct IntVector2(int x, int y)
{
    public int X = x;
    public int Y = y;

    public IntVector2(int value) : this(value, value) { }
}