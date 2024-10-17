using System.Drawing;
using System.Numerics;
using JoltPhysicsSharp;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;

namespace Swordfish.Graphics.Jolt;

internal class JoltDebugRenderer : DebugRenderer, IRenderStage
{
    private struct DrawRequest(Vector3 from, Vector3 to, Vector4 color)
    {
        public Vector3 From = from;
        public Vector3 To = to;
        public Vector4 Color = color;
    }

    private readonly DebugSettings _debugSettings;
    private readonly DrawSettings _drawSettings;
    private readonly ILineRenderer _lineRenderer;
    private readonly IJoltPhysics _joltPhysics;
    private List<DrawRequest> _drawBuffer = [];
    private List<Line> _lineCache = [];

    public JoltDebugRenderer(DebugSettings debugSettings, ILineRenderer lineRenderer, IJoltPhysics joltPhysics)
    {
        _debugSettings = debugSettings;
        _lineRenderer = lineRenderer;
        _joltPhysics = joltPhysics;

        _drawSettings = new DrawSettings
        {
            DrawShapeWireframe = true,
            DrawVelocity = true,
        };
    }

    public void Initialize(IRenderContext renderContext)
    {
    }

    public void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        if (_debugSettings.Gizmos.Physics)
        {
            _joltPhysics.System.DrawBodies(_drawSettings, this);
        }

        int drawRequestDifference = _drawBuffer.Count - _lineCache.Capacity;
        if (drawRequestDifference > 0)
        {
            _lineCache.Capacity = _drawBuffer.Count;
            for (int i = 0; i < drawRequestDifference; i++)
            {
                _lineCache.Add(_lineRenderer.CreateLine());
            }
        }

        for (int i = 0; i < _lineCache.Count; i++)
        {
            if (i < _drawBuffer.Count)
            {
                _lineCache[i].Start = _drawBuffer[i].From;
                _lineCache[i].End = _drawBuffer[i].To;
                _lineCache[i].Color = _drawBuffer[i].Color;
            }
            else
            {
                _lineCache[i].Start = Vector3.Zero;
                _lineCache[i].End = Vector3.Zero;
                _lineCache[i].Color = Vector4.Zero;
                //  TODO consider unallocating unused lines
            }
        }

        _drawBuffer.Clear();
    }

    public int Render(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        return 0;   //  Do nothing, the line renderer is doing the real work
    }

    protected override void DrawLine(Vector3 from, Vector3 to, uint color)
    {
        Color argb = Color.FromArgb((int)color);
        _drawBuffer.Add(new DrawRequest(from, to, new Vector4(argb.R, argb.G, argb.B, argb.A)));
    }

    protected override void DrawText3D(Vector3 position, string? text, uint color, float height = 0.5F)
    {
        return;
    }
}