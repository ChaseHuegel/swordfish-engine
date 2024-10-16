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
        for (int i = 0; i < _lines.Length; i++)
        {
            _lines[i] = lineRenderer.CreateLine(alwaysOnTop: true);
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

        const float BASE_SIZE = 0.85f;
        float scale = Vector3.Distance(pos, _camera.Transform.Position) * 0.1f;
        float size = BASE_SIZE * scale;
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
        Line handleXY1 = _lines[6];
        handleXY1.Start = pos + (right + up) * size - (up * handleSize);
        handleXY1.End = pos + (right + up) * size - ((right + up) * handleSize);
        handleXY1.Color = new Vector4(1, 1, 0, 1);

        Line handleXY2 = _lines[7];
        handleXY2.Start = pos + (right + up) * size - (right * handleSize);
        handleXY2.End = pos + (right + up) * size - ((right + up) * handleSize);
        handleXY2.Color = new Vector4(1, 1, 0, 1);

        //  XZ plane
        Line handleXZ1 = _lines[8];
        handleXZ1.Start = pos + (right + forward) * size - (forward * handleSize);
        handleXZ1.End = pos + (right + forward) * size - ((right + forward) * handleSize);
        handleXZ1.Color = new Vector4(1, 0, 1, 1);

        Line handleXZ2 = _lines[9];
        handleXZ2.Start = pos + (right + forward) * size - (right * handleSize);
        handleXZ2.End = pos + (right + forward) * size - ((right + forward) * handleSize);
        handleXZ2.Color = new Vector4(1, 0, 1, 1);

        //  ZY plane
        Line handleZY1 = _lines[10];
        handleZY1.Start = pos + (up + forward) * size - (forward * handleSize);
        handleZY1.End = pos + (up + forward) * size - ((up + forward) * handleSize);
        handleZY1.Color = new Vector4(0, 1, 1, 1);

        Line handleZY2 = _lines[11];
        handleZY2.Start = pos + (up + forward) * size - (up * handleSize);
        handleZY2.End = pos + (up + forward) * size - ((up + forward) * handleSize);
        handleZY2.Color = new Vector4(0, 1, 1, 1);

        //  XYZ handle
        Line handleXYZ = _lines[12];
        handleXYZ.Start = pos + right * handleSize;
        handleXYZ.End = pos + (right + up) * handleSize;
        handleXYZ.Color = new Vector4(1, 1, 1, 1);

        handleXYZ = _lines[13];
        handleXYZ.Start = pos + up * handleSize;
        handleXYZ.End = pos + (right + up) * handleSize;
        handleXYZ.Color = new Vector4(1, 1, 1, 1);

        handleXYZ = _lines[14];
        handleXYZ.Start = pos + forward * handleSize;
        handleXYZ.End = pos + (forward + up) * handleSize;
        handleXYZ.Color = new Vector4(1, 1, 1, 1);

        handleXYZ = _lines[15];
        handleXYZ.Start = pos + up * handleSize;
        handleXYZ.End = pos + (forward + up) * handleSize;
        handleXYZ.Color = new Vector4(1, 1, 1, 1);

        handleXYZ = _lines[16];
        handleXYZ.Start = pos + forward * handleSize;
        handleXYZ.End = pos + (forward + right) * handleSize;
        handleXYZ.Color = new Vector4(1, 1, 1, 1);

        handleXYZ = _lines[17];
        handleXYZ.Start = pos + right * handleSize;
        handleXYZ.End = pos + (forward + right) * handleSize;
        handleXYZ.Color = new Vector4(1, 1, 1, 1);
    }
}