using System.Numerics;
using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

public class Line : Handle
{
    public Vector3 Start;
    public Vector3 End;
    public Vector4 Color;

    private readonly ILineRenderer LineRenderer;

    internal Line(ILineRenderer lineRenderer, Vector3 start, Vector3 end, Vector4 color)
    {
        LineRenderer = lineRenderer;
        Start = start;
        End = end;
        Color = color;
    }

    protected override void OnDisposed()
    {
        LineRenderer.DeleteLine(this);
    }
}