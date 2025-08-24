namespace Reef;

public readonly struct IntRect
{
    public readonly IntVector2 Position;
    public readonly IntVector2 Size;
    public readonly int Left;
    public readonly int Top;
    public readonly int Right;
    public readonly int Bottom;

    public IntRect(IntVector2 position, IntVector2 size)
    {
        Position = position;
        Size = size;
        Left = position.X;
        Top = position.Y;
        Right = position.X + size.X - 1;
        Bottom = position.Y + size.Y - 1;
    }
    
    public IntRect(int left, int top, IntVector2 size)
    {
        Position = new IntVector2(left, top);
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
        
        Position = new IntVector2(left, top);
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