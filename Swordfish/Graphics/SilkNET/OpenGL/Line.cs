using System.Numerics;
using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

public class Line : Handle
{
    public Vector3 Start;
    public Vector3 End;
    public Vector4 Color;

    private readonly ILineRenderer _lineRenderer;

    internal Line(ILineRenderer lineRenderer, Vector3 start, Vector3 end, Vector4 color)
    {
        _lineRenderer = lineRenderer;
        Start = start;
        End = end;
        Color = color;
    }

    protected override void OnDisposed()
    {
        _lineRenderer.DeleteLine(this);
    }
}