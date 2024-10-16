using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;

namespace Swordfish.Demo;

public sealed class PositionGizmo : IDisposable
{
    private readonly Line[] _lines;

    public PositionGizmo(ILineRenderer lineRenderer)
    {
        _lines = new Line[9];
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
        const float SIZE = 2f;

        Vector3 pos = transform.Position;
        Vector3 forward = transform.GetForward();
        Vector3 up = transform.GetUp();
        Vector3 right = transform.GetRight();

        //  X axis
        Line line = _lines[0];
        line.Start = pos;
        line.End = pos + right * SIZE;
        line.Color = new Vector4(1, 0, 0, 1);

        Line armR = _lines[1];
        armR.Start = line.End;
        armR.End = line.End - Vector3.Normalize(right + forward) * 0.5f;
        armR.Color = line.Color;

        Line armL = _lines[2];
        armL.Start = line.End;
        armL.End = line.End - Vector3.Normalize(right - forward) * 0.5f;
        armL.Color = line.Color;

        //  Y axis
        line = _lines[3];
        line.Start = pos;
        line.End = pos + up * SIZE;
        line.Color = new Vector4(0, 1, 0, 1);

        armR = _lines[4];
        armR.Start = line.End;
        armR.End = line.End - Vector3.Normalize(up + right) * 0.5f;
        armR.Color = line.Color;

        armL = _lines[5];
        armL.Start = line.End;
        armL.End = line.End - Vector3.Normalize(up - right) * 0.5f;
        armL.Color = line.Color;

        //  Z axis
        line = _lines[6];
        line.Start = pos;
        line.End = pos + forward * SIZE;
        line.Color = new Vector4(0, 0, 1, 1);

        armR = _lines[7];
        armR.Start = line.End;
        armR.End = line.End - Vector3.Normalize(forward + right) * 0.5f;
        armR.Color = line.Color;

        armL = _lines[8];
        armL.Start = line.End;
        armL.End = line.End - Vector3.Normalize(forward - right) * 0.5f;
        armL.Color = line.Color;
    }
}