namespace WaywardBeyond.Client.Core.Numerics;

public record struct Float2
{
    public float X;
    public float Y;

    public float Min
    {
        get => X;
        set => X = value;
    }
    
    public float Max
    {
        get => Y;
        set => Y = value;
    }

    public float Length => Y - X;
    
    public Float2(float x, float y)
    {
        X = x;
        Y = y;
    }
}