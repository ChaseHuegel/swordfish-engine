using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;

namespace Swordfish.Demo;

public sealed class ScaleGizmo : IDisposable
{
    private readonly Line[] _lines;

    public ScaleGizmo(ILineRenderer lineRenderer)
    {
        _lines = new Line[18];
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

        const float SIZE = 0.75f;
        const float HANDLE_SIZE = SIZE * 0.5f;
        const float PLANE_START_SCALE = 1f - HANDLE_SIZE;
        const float PLANE_END_SCALE = HANDLE_SIZE;
        const float PLANE_START_SIZE = SIZE * PLANE_START_SCALE;
        const float PLANE_END_SIZE = SIZE * PLANE_END_SCALE;

        //  X axis
        Line xy = _lines[0];
        xy.Start = pos + right * SIZE;
        xy.End = xy.Start + up * SIZE;
        xy.Color = new Vector4(1, 0, 0, 1);

        Line xz = _lines[1];
        xz.Start = pos + right * SIZE;
        xz.End = xz.Start + forward * SIZE;
        xz.Color = new Vector4(1, 0, 0, 1);

        //  Y axis
        Line yx = _lines[2];
        yx.Start = pos + up * SIZE;
        yx.End = yx.Start + right * SIZE;
        yx.Color = new Vector4(0, 1, 0, 1);

        Line yz = _lines[3];
        yz.Start = pos + up * SIZE;
        yz.End = yz.Start + forward * SIZE;
        yz.Color = new Vector4(0, 1, 0, 1);

        //  Z axis
        Line zy = _lines[4];
        zy.Start = pos + forward * SIZE;
        zy.End = zy.Start + up * SIZE;
        zy.Color = new Vector4(0, 0, 1, 1);

        Line zx = _lines[5];
        zx.Start = pos + forward * SIZE;
        zx.End = zx.Start + right * SIZE;
        zx.Color = new Vector4(0, 0, 1, 1);

        //  XY plane
        Line handleXY1 = _lines[6];
        handleXY1.Start = xy.Start + up * PLANE_START_SIZE;
        handleXY1.End = handleXY1.Start - right * PLANE_END_SIZE;
        handleXY1.Color = new Vector4(1, 1, 0, 1);

        Line handleXY2 = _lines[7];
        handleXY2.Start = yx.Start + right * PLANE_START_SIZE;
        handleXY2.End = handleXY2.Start - up * PLANE_END_SIZE;
        handleXY2.Color = new Vector4(1, 1, 0, 1);

        //  XZ plane
        Line handleXZ1 = _lines[8];
        handleXZ1.Start = xy.Start + forward * PLANE_START_SIZE;
        handleXZ1.End = handleXZ1.Start - right * PLANE_END_SIZE;
        handleXZ1.Color = new Vector4(1, 0, 1, 1);

        Line handleXZ2 = _lines[9];
        handleXZ2.Start = zx.Start + right * PLANE_START_SIZE;
        handleXZ2.End = handleXZ2.Start - forward * PLANE_END_SIZE;
        handleXZ2.Color = new Vector4(1, 0, 1, 1);

        //  ZY plane
        Line handleZY1 = _lines[10];
        handleZY1.Start = zx.Start + up * PLANE_START_SIZE;
        handleZY1.End = handleZY1.Start - forward * PLANE_END_SIZE;
        handleZY1.Color = new Vector4(0, 1, 1, 1);

        Line handleZY2 = _lines[11];
        handleZY2.Start = yx.Start + forward * PLANE_START_SIZE;
        handleZY2.End = handleZY2.Start - up * PLANE_END_SIZE;
        handleZY2.Color = new Vector4(0, 1, 1, 1);

        //  XYZ handle
        Line handleXYZ = _lines[12];
        handleXYZ.Start = pos + right * HANDLE_SIZE;
        handleXYZ.End = pos + (right + up) * HANDLE_SIZE;
        handleXYZ.Color = new Vector4(1, 1, 1, 1);

        handleXYZ = _lines[13];
        handleXYZ.Start = pos + up * HANDLE_SIZE;
        handleXYZ.End = pos + (right + up) * HANDLE_SIZE;
        handleXYZ.Color = new Vector4(1, 1, 1, 1);

        handleXYZ = _lines[14];
        handleXYZ.Start = pos + forward * HANDLE_SIZE;
        handleXYZ.End = pos + (forward + up) * HANDLE_SIZE;
        handleXYZ.Color = new Vector4(1, 1, 1, 1);

        handleXYZ = _lines[15];
        handleXYZ.Start = pos + up * HANDLE_SIZE;
        handleXYZ.End = pos + (forward + up) * HANDLE_SIZE;
        handleXYZ.Color = new Vector4(1, 1, 1, 1);

        handleXYZ = _lines[16];
        handleXYZ.Start = pos + forward * HANDLE_SIZE;
        handleXYZ.End = pos + (forward + right) * HANDLE_SIZE;
        handleXYZ.Color = new Vector4(1, 1, 1, 1);

        handleXYZ = _lines[17];
        handleXYZ.Start = pos + right * HANDLE_SIZE;
        handleXYZ.End = pos + (forward + right) * HANDLE_SIZE;
        handleXYZ.Color = new Vector4(1, 1, 1, 1);
    }
}