using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;

namespace Swordfish.Demo;

public sealed class ScaleGizmo : IDisposable
{
    private readonly Line[] _lines;
    private readonly Camera _camera;

    public ScaleGizmo(ILineRenderer lineRenderer, Camera camera)
    {
        _camera = camera;

        _lines = new Line[18];
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

        const float baseSize = 0.85f;
        float scale = Vector3.Distance(pos, _camera.Transform.Position) * 0.1f;
        float size = baseSize * scale;
        float handleSize = 0.35f * scale;

        //  X axis
        Line xy = _lines[0];
        xy.Start = pos + right * size;
        xy.End = xy.Start + up * size;
        xy.Color = new Vector4(1, 0, 0, 1);

        Line xz = _lines[1];
        xz.Start = pos + right * size;
        xz.End = xz.Start + forward * size;
        xz.Color = new Vector4(1, 0, 0, 1);

        //  Y axis
        Line yx = _lines[2];
        yx.Start = pos + up * size;
        yx.End = yx.Start + right * size;
        yx.Color = new Vector4(0, 1, 0, 1);

        Line yz = _lines[3];
        yz.Start = pos + up * size;
        yz.End = yz.Start + forward * size;
        yz.Color = new Vector4(0, 1, 0, 1);

        //  Z axis
        Line zy = _lines[4];
        zy.Start = pos + forward * size;
        zy.End = zy.Start + up * size;
        zy.Color = new Vector4(0, 0, 1, 1);

        Line zx = _lines[5];
        zx.Start = pos + forward * size;
        zx.End = zx.Start + right * size;
        zx.Color = new Vector4(0, 0, 1, 1);

        //  XY plane
        Line handleXy1 = _lines[6];
        handleXy1.Start = pos + (right + up) * size - (up * handleSize);
        handleXy1.End = pos + (right + up) * size - ((right + up) * handleSize);
        handleXy1.Color = new Vector4(1, 1, 0, 1);

        Line handleXy2 = _lines[7];
        handleXy2.Start = pos + (right + up) * size - (right * handleSize);
        handleXy2.End = pos + (right + up) * size - ((right + up) * handleSize);
        handleXy2.Color = new Vector4(1, 1, 0, 1);

        //  XZ plane
        Line handleXz1 = _lines[8];
        handleXz1.Start = pos + (right + forward) * size - (forward * handleSize);
        handleXz1.End = pos + (right + forward) * size - ((right + forward) * handleSize);
        handleXz1.Color = new Vector4(1, 0, 1, 1);

        Line handleXz2 = _lines[9];
        handleXz2.Start = pos + (right + forward) * size - (right * handleSize);
        handleXz2.End = pos + (right + forward) * size - ((right + forward) * handleSize);
        handleXz2.Color = new Vector4(1, 0, 1, 1);

        //  ZY plane
        Line handleZy1 = _lines[10];
        handleZy1.Start = pos + (up + forward) * size - (forward * handleSize);
        handleZy1.End = pos + (up + forward) * size - ((up + forward) * handleSize);
        handleZy1.Color = new Vector4(0, 1, 1, 1);

        Line handleZy2 = _lines[11];
        handleZy2.Start = pos + (up + forward) * size - (up * handleSize);
        handleZy2.End = pos + (up + forward) * size - ((up + forward) * handleSize);
        handleZy2.Color = new Vector4(0, 1, 1, 1);

        //  XYZ handle
        Line handleXyz = _lines[12];
        handleXyz.Start = pos + right * handleSize;
        handleXyz.End = pos + (right + up) * handleSize;
        handleXyz.Color = new Vector4(1, 1, 1, 1);

        handleXyz = _lines[13];
        handleXyz.Start = pos + up * handleSize;
        handleXyz.End = pos + (right + up) * handleSize;
        handleXyz.Color = new Vector4(1, 1, 1, 1);

        handleXyz = _lines[14];
        handleXyz.Start = pos + forward * handleSize;
        handleXyz.End = pos + (forward + up) * handleSize;
        handleXyz.Color = new Vector4(1, 1, 1, 1);

        handleXyz = _lines[15];
        handleXyz.Start = pos + up * handleSize;
        handleXyz.End = pos + (forward + up) * handleSize;
        handleXyz.Color = new Vector4(1, 1, 1, 1);

        handleXyz = _lines[16];
        handleXyz.Start = pos + forward * handleSize;
        handleXyz.End = pos + (forward + right) * handleSize;
        handleXyz.Color = new Vector4(1, 1, 1, 1);

        handleXyz = _lines[17];
        handleXyz.Start = pos + right * handleSize;
        handleXyz.End = pos + (forward + right) * handleSize;
        handleXyz.Color = new Vector4(1, 1, 1, 1);
    }
}