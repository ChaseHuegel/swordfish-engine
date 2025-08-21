namespace Reef.UI;

public struct CornerRadius(int topLeft, int topRight, int bottomLeft, int bottomRight)
{
    public int TopLeft = topLeft;
    public int TopRight = topRight;
    public int BottomLeft = bottomLeft;
    public int BottomRight = bottomRight;

    public CornerRadius(int value) : this(value, value, value, value) { }
}