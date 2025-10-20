using System.Numerics;
using JoltPhysicsSharp;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;

namespace Swordfish.Graphics.Jolt;

internal class JoltDebugRenderer(in DebugSettings debugSettings, in ILineRenderer lineRenderer, in IJoltPhysics joltPhysics)
    : DebugRenderer, IWorldSpaceRenderStage
{
    private struct DrawRequest(Vector3 from, Vector3 to, Vector4 color)
    {
        public readonly Vector3 From = from;
        public readonly Vector3 To = to;
        public readonly Vector4 Color = color;
    }

    private readonly List<DrawRequest> _drawBuffer = [];
    private readonly List<Line> _lineCache = [];
    private readonly DebugSettings _debugSettings = debugSettings;
    private readonly ILineRenderer _lineRenderer = lineRenderer;
    private readonly IJoltPhysics _joltPhysics = joltPhysics;
    private readonly DrawSettings _drawSettings = new()
    {
        DrawShapeWireframe = true,
        DrawVelocity = true,
    };
    
    public void Initialize(IRenderContext renderContext)
    {
    }

    public void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection, bool isDepthPass)
    {
        if (isDepthPass)
        {
            return;
        }
        
        if (_debugSettings.Gizmos.Physics)
        {
            _joltPhysics.System.DrawBodies(_drawSettings, this);
        }

        int drawRequestDifference = _drawBuffer.Count - _lineCache.Capacity;
        if (drawRequestDifference > 0)
        {
            _lineCache.Capacity = _drawBuffer.Count;
            for (var i = 0; i < drawRequestDifference; i++)
            {
                _lineCache.Add(_lineRenderer.CreateLine());
            }
        }

        for (var i = 0; i < _lineCache.Count; i++)
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

    public int Render(double delta, Matrix4x4 view, Matrix4x4 projection, Action<ShaderProgram> shaderActivationCallback, bool isDepthPass)
    {
        return 0;   //  Do nothing, the line renderer is doing the real work
    }

    protected override void DrawLine(Vector3 from, Vector3 to, JoltColor color)
    {
        _drawBuffer.Add(new DrawRequest(from, to, new Vector4(color.R, color.G, color.B, color.A)));
    }

    protected override void DrawText3D(Vector3 position, string? text, JoltColor color, float height = 0.5f)
    {
        throw new NotImplementedException();
    }
}