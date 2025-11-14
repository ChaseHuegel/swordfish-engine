namespace WaywardBeyond.Client.Core.Numerics;

public record struct Int2
{
    public int X;
    public int Y;

    public int Min
    {
        get => X;
        set => X = value;
    }
    
    public int Max
    {
        get => Y;
        set => Y = value;
    }
    
    public Int2(int x, int y)
    {
        X = x;
        Y = y;
    }
}