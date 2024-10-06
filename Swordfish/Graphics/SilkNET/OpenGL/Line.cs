using System.Numerics;

public class Line
{
    public Vector3 Start;
    public Vector3 End;
    public Vector4 Color;

    public Line(Vector3 start, Vector3 end, Vector4 color)
    {
        Start = start;
        End = end;
        Color = color;
    }
}