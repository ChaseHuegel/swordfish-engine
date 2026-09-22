namespace WaywardBeyond.Client.Core.Numerics;

public record struct Short3
{
    public short X;
    public short Y;
    public short Z;
        
    public Short3(short x, short y, short z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}