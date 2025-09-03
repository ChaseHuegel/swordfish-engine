namespace Reef.UI;

public struct Padding(int left, int top, int right, int bottom)
{
    public int Left = left;
    public int Top = top;
    public int Right = right;
    public int Bottom = bottom;

    public Padding(int value) : this(value, value, value, value) { }
}