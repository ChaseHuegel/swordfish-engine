using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Extensions;

namespace Swordfish.Demo;

public sealed class OrientationGizmo : IDisposable
{
    private readonly Line[] _lines;
    private readonly Camera _camera;

    public OrientationGizmo(ILineRenderer lineRenderer, Camera camera)
    {
        _camera = camera;

        _lines = new Line[90];
        for (var i = 0; i < _lines.Length; i++)
        {
            _lines[i] = lineRenderer.CreateLine(alwaysOnTop: true);
        }
    }

    public void Dispose()
    {
        for (var i = 0; i < _lines.Length; i++)
        {
            _lines[i].Dispose();
        }
    }

    public void Render(TransformComponent transform)
    {
        Vector3 pos = transform.Position;
        Vector3 forward = transform.GetForward();
        Vector3 up = transform.GetUp();
        Vector3 right = transform.GetRight();

        const float baseSize = 1.25f;
        const int segmentsPerAxis = 30;
        const float segmentFactor = 1f / segmentsPerAxis;
        float scale = Vector3.Distance(pos, _camera.Transform.Read().Position) * 0.1f;
        float size = baseSize * scale;

        //  X axis
        for (var i = 0; i < segmentsPerAxis; i++)
        {
            int segmentIndex = i;
            Vector3 arcStart = right.Slerp(up, i * segmentFactor);
            Vector3 arcEnd = right.Slerp(up, (i + 1) * segmentFactor);

            Line line = _lines[segmentIndex];
            line.Start = pos + arcStart * size;
            line.End = pos + arcEnd * size;
            line.Color = new Vector4(1, 0, 0, 1);
        }

        //  Y axis
        for (var i = 0; i < segmentsPerAxis; i++)
        {
            int segmentIndex = i + 30;
            Vector3 arcStart = up.Slerp(forward, i * segmentFactor);
            Vector3 arcEnd = up.Slerp(forward, (i + 1) * segmentFactor);

            Line line = _lines[segmentIndex];
            line.Start = pos + arcStart * size;
            line.End = pos + arcEnd * size;
            line.Color = new Vector4(0, 1, 0, 1);
        }

        //  Z axis
        for (var i = 0; i < segmentsPerAxis; i++)
        {
            int segmentIndex = i + 60;
            Vector3 arcStart = forward.Slerp(right, i * segmentFactor);
            Vector3 arcEnd = forward.Slerp(right, (i + 1) * segmentFactor);

            Line line = _lines[segmentIndex];
            line.Start = pos + arcStart * size;
            line.End = pos + arcEnd * size;
            line.Color = new Vector4(0, 0, 1, 1);
        }
    }
}