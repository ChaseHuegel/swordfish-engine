namespace Reef.UI;

public struct CornerRadius(int left, int top, int right, int bottom)
{
    public int Left = left;
    public int Top = top;
    public int Right = right;
    public int Bottom = bottom;

    public CornerRadius(int value) : this(value, value, value, value) { }
}