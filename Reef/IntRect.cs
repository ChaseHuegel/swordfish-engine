namespace Reef;

public readonly struct IntRect
{
    public readonly Anchor Anchor;
    public readonly IntVector2 Position;
    public readonly IntVector2 Size;
    public readonly int Left;
    public readonly int Top;
    public readonly int Right;
    public readonly int Bottom;

    public IntRect(IntVector2 position, IntVector2 size, Anchor anchor = Anchor.TopLeft)
    {
        Position = position;
        Size = size;
        Anchor = anchor;

        switch (anchor)
        {
            case Anchor.TopLeft:
                Left = position.X;
                Top = position.Y;
                Right = position.X + size.X - 1;
                Bottom = position.Y + size.Y - 1;
                break;
            case Anchor.Center:
                int halfWidth = size.X >> 1;
                int halfHeight = size.Y >> 1;
                Left = position.X - halfWidth;
                Top = position.Y - halfHeight;
                Right = position.X + halfWidth - 1;
                Bottom = position.Y + halfHeight - 1;
                break;
        }
    }
    
    public IntRect(int left, int top, IntVector2 size)
    {
        int halfWidth = size.X >> 1;
        int halfHeight = size.Y >> 1;
        
        Anchor = Anchor.TopLeft;
        Position = new IntVector2(left + halfWidth, top + halfHeight);
        Size = size;
        Left = left;
        Top = top;
        Right = left + size.X - 1;
        Bottom = top + size.Y - 1;
    }
    
    public IntRect(int left, int top, int right, int bottom, Anchor anchor = Anchor.TopLeft)
    {
        int width = right - left + 1;
        int height = bottom - top + 1;
        
        Size = new IntVector2(width, height);
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
        
        switch (anchor)
        {
            case Anchor.TopLeft:
                Position = new IntVector2(left, top);
                break;
            case Anchor.TopRight:
                Position = new IntVector2(left + width - 1, top + height - 1);
                break;
            case Anchor.Center:
                int halfWidth = width >> 1;
                int halfHeight = height >> 1;
                Position = new IntVector2(left + halfWidth - 1, top + halfHeight - 1);
                break;
        }
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