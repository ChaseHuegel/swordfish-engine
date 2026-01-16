using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Types;

namespace Swordfish.Demo;

public sealed class PositionGizmo : IDisposable
{
    private readonly Line[] _lines;
    private readonly DataBinding<Camera> _camera;

    public PositionGizmo(ILineRenderer lineRenderer, DataBinding<Camera> camera)
    {
        _camera = camera;

        _lines = new Line[9];
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

        const float baseSize = 2f;
        float scale = Vector3.Distance(pos, _camera.Get().Transform.Position) * 0.1f;
        float size = baseSize * scale;
        float armSize = 0.5f * scale;

        //  X axis
        Line line = _lines[0];
        line.Start = pos;
        line.End = pos + right * size;
        line.Color = new Vector4(1, 0, 0, 1);

        Line armR = _lines[1];
        armR.Start = line.End;
        armR.End = line.End - Vector3.Normalize(right + forward) * armSize;
        armR.Color = line.Color;

        Line armL = _lines[2];
        armL.Start = line.End;
        armL.End = line.End - Vector3.Normalize(right - forward) * armSize;
        armL.Color = line.Color;

        //  Y axis
        line = _lines[3];
        line.Start = pos;
        line.End = pos + up * size;
        line.Color = new Vector4(0, 1, 0, 1);

        armR = _lines[4];
        armR.Start = line.End;
        armR.End = line.End - Vector3.Normalize(up + right) * armSize;
        armR.Color = line.Color;

        armL = _lines[5];
        armL.Start = line.End;
        armL.End = line.End - Vector3.Normalize(up - right) * armSize;
        armL.Color = line.Color;

        //  Z axis
        line = _lines[6];
        line.Start = pos;
        line.End = pos + forward * size;
        line.Color = new Vector4(0, 0, 1, 1);

        armR = _lines[7];
        armR.Start = line.End;
        armR.End = line.End - Vector3.Normalize(forward + right) * armSize;
        armR.Color = line.Color;

        armL = _lines[8];
        armL.Start = line.End;
        armL.End = line.End - Vector3.Normalize(forward - right) * armSize;
        armL.Color = line.Color;
    }
}