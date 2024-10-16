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

    public OrientationGizmo(ILineRenderer lineRenderer)
    {
        _lines = new Line[90];
        for (int i = 0; i < _lines.Length; i++)
        {
            _lines[i] = lineRenderer.CreateLine();
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < _lines.Length; i++)
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

        const float SIZE = 1.25f;
        const int SEGMENTS_PER_AXIS = 30;
        const float SEGMENT_FACTOR = 1f / SEGMENTS_PER_AXIS;

        //  X axis
        for (int i = 0; i < SEGMENTS_PER_AXIS; i++)
        {
            int segmentIndex = i;
            Vector3 arcStart = right.Slerp(up, i * SEGMENT_FACTOR);
            Vector3 arcEnd = right.Slerp(up, (i + 1) * SEGMENT_FACTOR);

            Line line = _lines[segmentIndex];
            line.Start = pos + arcStart * SIZE;
            line.End = pos + arcEnd * SIZE;
            line.Color = new Vector4(1, 0, 0, 1);
        }

        //  Y axis
        for (int i = 0; i < SEGMENTS_PER_AXIS; i++)
        {
            int segmentIndex = i + 30;
            Vector3 arcStart = up.Slerp(forward, i * SEGMENT_FACTOR);
            Vector3 arcEnd = up.Slerp(forward, (i + 1) * SEGMENT_FACTOR);

            Line line = _lines[segmentIndex];
            line.Start = pos + arcStart * SIZE;
            line.End = pos + arcEnd * SIZE;
            line.Color = new Vector4(0, 1, 0, 1);
        }

        //  Z axis
        for (int i = 0; i < SEGMENTS_PER_AXIS; i++)
        {
            int segmentIndex = i + 60;
            Vector3 arcStart = forward.Slerp(right, i * SEGMENT_FACTOR);
            Vector3 arcEnd = forward.Slerp(right, (i + 1) * SEGMENT_FACTOR);

            Line line = _lines[segmentIndex];
            line.Start = pos + arcStart * SIZE;
            line.End = pos + arcEnd * SIZE;
            line.Color = new Vector4(0, 0, 1, 1);
        }
    }
}