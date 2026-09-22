namespace WaywardBeyond.Client.Core.Numerics;

public record struct Int3
{
    public int X;
    public int Y;
    public int Z;
        
    public Int3(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}