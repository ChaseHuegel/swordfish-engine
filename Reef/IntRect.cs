namespace Reef;

public readonly struct IntRect
{
    public readonly IntVector2 Center;
    public readonly IntVector2 Size;
    public readonly int Left;
    public readonly int Top;
    public readonly int Right;
    public readonly int Bottom;

    public IntRect(IntVector2 center, IntVector2 size)
    {
        int halfWidth = size.X >> 1;
        int halfHeight = size.Y >> 1;
        
        Center = center;
        Size = size;
        Left = center.X - halfWidth;
        Top = center.Y - halfHeight;
        Right = center.X + halfWidth - 1;
        Bottom = center.Y + halfHeight - 1;
    }
    
    public IntRect(int left, int top, IntVector2 size)
    {
        int halfWidth = size.X >> 1;
        int halfHeight = size.Y >> 1;
        
        Center = new IntVector2(left + halfWidth, top + halfHeight);
        Size = size;
        Left = left;
        Top = top;
        Right = left + size.X - 1;
        Bottom = top + size.Y - 1;
    }
    
    public IntRect(int left, int top, int right, int bottom)
    {
        int width = right - left + 1;
        int height = bottom - top + 1;
        int halfWidth = width >> 1;
        int halfHeight = height >> 1;
        
        Center = new IntVector2(left + halfWidth, top + halfHeight);
        Size = new IntVector2(width, height);
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
    
    public bool Intersects(IntRect other)
    {
        if (other.Left > Right)
        {
            return false;
        }
        
        if (other.Right < Left)
        {
            return false;
        }
        
        if (other.Top > Bottom)
        {
            return false;
        }
        
        if (other.Bottom < Top)
        {
            return false;
        }

        return true;
    }
}