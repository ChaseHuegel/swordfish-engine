using System;
using System.Collections.Generic;
using System.Numerics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Meshing;

namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Generates screen-sized 3D Translate, Rotate, and Scale gizmo overlay meshes.</summary>
public static class TransformGizmoMeshBuilder
{
    private const float DEGREES_TO_RADIANS = MathF.PI / 180f;

    /// <summary>Computes a camera-distance-proportional gizmo world size so handles remain crisp on screen.</summary>
    public static float ComputeScreenConstantScale(Vector3 origin, PreviewCamera camera)
    {
        float dist = MathF.Max(Vector3.Distance(camera.GetEyePosition(), origin), 0.14f);
        float halfTan = MathF.Tan(Math.Clamp(camera.FovDegrees, 15f, 120f) * 0.5f * DEGREES_TO_RADIANS);
        return Math.Clamp(dist * halfTan * 0.25f, 0.04f, 12f);
    }

    /// <summary>Extracts normalized X, Y, Z basis vectors from <paramref name="basisRotation"/>.</summary>
    public static (Vector3 X, Vector3 Y, Vector3 Z) ExtractBasisAxes(in Matrix4x4 basisRotation)
    {
        Vector3 x = SafeNormalize(new Vector3(basisRotation.M11, basisRotation.M12, basisRotation.M13), Vector3.UnitX);
        Vector3 y = SafeNormalize(new Vector3(basisRotation.M21, basisRotation.M22, basisRotation.M23), Vector3.UnitY);
        Vector3 z = SafeNormalize(new Vector3(basisRotation.M31, basisRotation.M32, basisRotation.M33), Vector3.UnitZ);
        return (x, y, z);
    }

    /// <summary>Computes an on-screen world-space anchor for the gizmo, falling back to mesh center when the first-person pivot projects off-screen.</summary>
    public static Vector3 ResolveGizmoAnchor(
        Vector3 position,
        Vector3 rotationDegrees,
        Vector3 scale,
        in VoxelMesh mesh,
        PreviewCamera camera,
        float viewportWidth,
        float viewportHeight)
    {
        if (!camera.IsFirstPerson || viewportWidth <= 2f || viewportHeight <= 2f)
        {
            return position;
        }

        if (camera.TryProjectToScreen(position, viewportWidth, viewportHeight, out Vector2 sp, out _)
            && sp.X >= viewportWidth * 0.08f
            && sp.X <= viewportWidth * 0.92f
            && sp.Y >= viewportHeight * 0.08f
            && sp.Y <= viewportHeight * 0.92f)
        {
            return position;
        }

        if (!mesh.IsEmpty && mesh.Vertices != null && mesh.Vertices.Length > 0)
        {
            Vector3[] verts = mesh.Vertices;
            Vector3 min = verts[0];
            Vector3 max = verts[0];
            for (var i = 1; i < verts.Length; i++)
            {
                min = Vector3.Min(min, verts[i]);
                max = Vector3.Max(max, verts[i]);
            }

            Vector3 localCenter = (min + max) * 0.5f;
            Matrix4x4 model = ModelTransformMath.CreateModelMatrix(position, rotationDegrees, scale);
            Vector3 worldCenter = Vector3.Transform(localCenter, model);
            if (camera.TryProjectToScreen(worldCenter, viewportWidth, viewportHeight, out _, out _))
            {
                return worldCenter;
            }
        }

        return position;
    }

    /// <summary>Builds depth-cleared overlay meshes representing the requested gizmo mode, basis, and highlight state.</summary>
    public static IReadOnlyList<PreviewSceneMesh> BuildMeshes(
        TransformGizmoMode mode,
        Vector3 origin,
        in Matrix4x4 basisRotation,
        float gizmoScale,
        TransformGizmoAxis hoveredAxis,
        TransformGizmoAxis activeAxis)
    {
        float s = MathF.Max(gizmoScale, 0.03f);
        (Vector3 axisX, Vector3 axisY, Vector3 axisZ) = ExtractBasisAxes(in basisRotation);

        var verts = new List<Vector3>(512);
        var norms = new List<Vector3>(512);
        var uvs = new List<Vector3>(512);
        var cols = new List<Vector4>(512);
        var tris = new List<uint>(1024);

        var blendVerts = new List<Vector3>(24);
        var blendNorms = new List<Vector3>(24);
        var blendUvs = new List<Vector3>(24);
        var blendCols = new List<Vector4>(24);
        var blendTris = new List<uint>(36);

        Vector4 colX = ResolveAxisColor(TransformGizmoAxis.X, new Vector4(0.95f, 0.28f, 0.28f, 1f), hoveredAxis, activeAxis);
        Vector4 colY = ResolveAxisColor(TransformGizmoAxis.Y, new Vector4(0.18f, 0.80f, 0.42f, 1f), hoveredAxis, activeAxis);
        Vector4 colZ = ResolveAxisColor(TransformGizmoAxis.Z, new Vector4(0.28f, 0.56f, 0.98f, 1f), hoveredAxis, activeAxis);
        Vector4 colCenter = ResolveAxisColor(TransformGizmoAxis.Uniform, new Vector4(0.90f, 0.93f, 0.98f, 1f), hoveredAxis, activeAxis);

        switch (mode)
        {
            case TransformGizmoMode.Translate:
                BuildAxisArrow(verts, norms, uvs, cols, tris, origin, axisX, s, colX);
                BuildAxisArrow(verts, norms, uvs, cols, tris, origin, axisY, s, colY);
                BuildAxisArrow(verts, norms, uvs, cols, tris, origin, axisZ, s, colZ);
                AppendAxisCube(verts, norms, uvs, cols, tris, origin, axisX, axisY, axisZ, 0.065f * s, new Vector4(0.88f, 0.91f, 0.96f, 1f));

                BuildPlanarHandle(verts, norms, uvs, cols, tris, blendVerts, blendNorms, blendUvs, blendCols, blendTris,
                    origin, axisX, axisY, s, TransformGizmoAxis.XY, new Vector4(0.35f, 0.58f, 0.98f, 0.34f), hoveredAxis, activeAxis);
                BuildPlanarHandle(verts, norms, uvs, cols, tris, blendVerts, blendNorms, blendUvs, blendCols, blendTris,
                    origin, axisX, axisZ, s, TransformGizmoAxis.XZ, new Vector4(0.24f, 0.82f, 0.46f, 0.34f), hoveredAxis, activeAxis);
                BuildPlanarHandle(verts, norms, uvs, cols, tris, blendVerts, blendNorms, blendUvs, blendCols, blendTris,
                    origin, axisY, axisZ, s, TransformGizmoAxis.YZ, new Vector4(0.96f, 0.34f, 0.34f, 0.34f), hoveredAxis, activeAxis);
                break;

            case TransformGizmoMode.Rotate:
                BuildRotationRing(verts, norms, uvs, cols, tris, origin, axisY, axisZ, axisX, 0.92f * s, 0.024f * s, colX);
                BuildRotationRing(verts, norms, uvs, cols, tris, origin, axisX, axisZ, axisY, 0.92f * s, 0.024f * s, colY);
                BuildRotationRing(verts, norms, uvs, cols, tris, origin, axisX, axisY, axisZ, 0.92f * s, 0.024f * s, colZ);
                AppendAxisCube(verts, norms, uvs, cols, tris, origin, axisX, axisY, axisZ, 0.055f * s, new Vector4(0.88f, 0.91f, 0.96f, 1f));
                break;

            case TransformGizmoMode.Scale:
                BuildScaleStem(verts, norms, uvs, cols, tris, origin, axisX, axisY, axisZ, axisX, s, colX);
                BuildScaleStem(verts, norms, uvs, cols, tris, origin, axisX, axisY, axisZ, axisY, s, colY);
                BuildScaleStem(verts, norms, uvs, cols, tris, origin, axisX, axisY, axisZ, axisZ, s, colZ);
                AppendAxisCube(verts, norms, uvs, cols, tris, origin, axisX, axisY, axisZ, 0.095f * s, colCenter);
                break;
        }

        var result = new List<PreviewSceneMesh>(2)
        {
            new(
                new VoxelMesh([.. tris], [.. verts], [.. cols], [.. uvs], [.. norms]),
                Matrix4x4.Identity,
                BrickAlpha.Opaque,
                0f,
                doubleSided: true,
                textureMode: PreviewTextureMode.VertexColorOnly,
                isOverlay: true),
        };

        if (blendTris.Count > 0)
        {
            result.Add(new PreviewSceneMesh(
                new VoxelMesh([.. blendTris], [.. blendVerts], [.. blendCols], [.. blendUvs], [.. blendNorms]),
                Matrix4x4.Identity,
                BrickAlpha.Blend,
                0f,
                doubleSided: true,
                textureMode: PreviewTextureMode.VertexColorOnly,
                isOverlay: true));
        }

        return result;
    }

    private static Vector4 ResolveAxisColor(
        TransformGizmoAxis handleAxis,
        Vector4 baseColor,
        TransformGizmoAxis hoveredAxis,
        TransformGizmoAxis activeAxis)
    {
        if (activeAxis == handleAxis)
        {
            return new Vector4(0.98f, 0.80f, 0.14f, baseColor.W >= 0.99f ? 1f : 0.65f);
        }

        if (hoveredAxis == handleAxis)
        {
            return new Vector4(
                MathF.Min(1f, baseColor.X + 0.28f),
                MathF.Min(1f, baseColor.Y + 0.28f),
                MathF.Min(1f, baseColor.Z + 0.28f),
                baseColor.W >= 0.99f ? 1f : 0.56f);
        }

        return baseColor;
    }

    private static void BuildAxisArrow(
        List<Vector3> verts,
        List<Vector3> norms,
        List<Vector3> uvs,
        List<Vector4> cols,
        List<uint> tris,
        Vector3 origin,
        Vector3 dir,
        float scale,
        Vector4 color)
    {
        Vector3 shaftStart = origin + (dir * (0.08f * scale));
        Vector3 shaftEnd = origin + (dir * (0.94f * scale));
        PreviewGeometryFactory.AppendBoxLine(verts, norms, uvs, cols, tris, shaftStart, shaftEnd, 0.022f * scale, 0.022f * scale, color);

        Vector3 coneTip = origin + (dir * (1.18f * scale));
        AppendCone(verts, norms, uvs, cols, tris, shaftEnd, coneTip, dir, 0.072f * scale, color);
    }

    private static void BuildScaleStem(
        List<Vector3> verts,
        List<Vector3> norms,
        List<Vector3> uvs,
        List<Vector4> cols,
        List<uint> tris,
        Vector3 origin,
        Vector3 basisX,
        Vector3 basisY,
        Vector3 basisZ,
        Vector3 dir,
        float scale,
        Vector4 color)
    {
        Vector3 start = origin + (dir * (0.11f * scale));
        Vector3 end = origin + (dir * (0.90f * scale));
        PreviewGeometryFactory.AppendBoxLine(verts, norms, uvs, cols, tris, start, end, 0.022f * scale, 0.022f * scale, color);
        Vector3 tipCenter = origin + (dir * (0.96f * scale));
        AppendAxisCube(verts, norms, uvs, cols, tris, tipCenter, basisX, basisY, basisZ, 0.070f * scale, color);
    }

    private static void BuildPlanarHandle(
        List<Vector3> lineVerts,
        List<Vector3> lineNorms,
        List<Vector3> lineUvs,
        List<Vector4> lineCols,
        List<uint> lineTris,
        List<Vector3> fillVerts,
        List<Vector3> fillNorms,
        List<Vector3> fillUvs,
        List<Vector4> fillCols,
        List<uint> fillTris,
        Vector3 origin,
        Vector3 uAxis,
        Vector3 vAxis,
        float scale,
        TransformGizmoAxis handleAxis,
        Vector4 baseFill,
        TransformGizmoAxis hoveredAxis,
        TransformGizmoAxis activeAxis)
    {
        float min = 0.24f * scale;
        float max = 0.48f * scale;
        Vector3 p00 = origin + (uAxis * min) + (vAxis * min);
        Vector3 p10 = origin + (uAxis * max) + (vAxis * min);
        Vector3 p11 = origin + (uAxis * max) + (vAxis * max);
        Vector3 p01 = origin + (uAxis * min) + (vAxis * max);

        Vector4 fillColor = ResolveAxisColor(handleAxis, baseFill, hoveredAxis, activeAxis);
        uint fBase = (uint)fillVerts.Count;
        fillVerts.Add(p00);
        fillVerts.Add(p10);
        fillVerts.Add(p11);
        fillVerts.Add(p01);
        for (var i = 0; i < 4; i++)
        {
            fillNorms.Add(Vector3.UnitY);
            fillUvs.Add(Vector3.Zero);
            fillCols.Add(fillColor);
        }

        fillTris.Add(fBase + 0);
        fillTris.Add(fBase + 1);
        fillTris.Add(fBase + 2);
        fillTris.Add(fBase + 0);
        fillTris.Add(fBase + 2);
        fillTris.Add(fBase + 3);

        var borderCol = new Vector4(fillColor.X, fillColor.Y, fillColor.Z, 1f);
        float bw = 0.011f * scale;
        PreviewGeometryFactory.AppendBoxLine(lineVerts, lineNorms, lineUvs, lineCols, lineTris, p10, p11, bw, bw, borderCol);
        PreviewGeometryFactory.AppendBoxLine(lineVerts, lineNorms, lineUvs, lineCols, lineTris, p11, p01, bw, bw, borderCol);
    }

    private static void BuildRotationRing(
        List<Vector3> verts,
        List<Vector3> norms,
        List<Vector3> uvs,
        List<Vector4> cols,
        List<uint> tris,
        Vector3 origin,
        Vector3 uAxis,
        Vector3 vAxis,
        Vector3 normalAxis,
        float radius,
        float tubeHalf,
        Vector4 color)
    {
        const int segments = 36;
        for (var i = 0; i < segments; i++)
        {
            float a0 = (i / (float)segments) * MathF.Tau;
            float a1 = ((i + 1) / (float)segments) * MathF.Tau;

            Vector3 dir0 = (uAxis * MathF.Cos(a0)) + (vAxis * MathF.Sin(a0));
            Vector3 dir1 = (uAxis * MathF.Cos(a1)) + (vAxis * MathF.Sin(a1));
            Vector3 p0 = origin + (dir0 * radius);
            Vector3 p1 = origin + (dir1 * radius);

            Vector3 r0 = dir0 * tubeHalf;
            Vector3 r1 = dir1 * tubeHalf;
            Vector3 n = normalAxis * tubeHalf;

            uint b = (uint)verts.Count;
            verts.Add(p0 - r0 - n); // 0
            verts.Add(p0 + r0 - n); // 1
            verts.Add(p0 + r0 + n); // 2
            verts.Add(p0 - r0 + n); // 3
            verts.Add(p1 - r1 - n); // 4
            verts.Add(p1 + r1 - n); // 5
            verts.Add(p1 + r1 + n); // 6
            verts.Add(p1 - r1 + n); // 7

            for (var k = 0; k < 8; k++)
            {
                norms.Add(normalAxis);
                uvs.Add(Vector3.Zero);
                cols.Add(color);
            }

            uint[] segTris =
            [
                3, 2, 6, 3, 6, 7,
                0, 5, 1, 0, 4, 5,
                1, 5, 6, 1, 6, 2,
                0, 3, 7, 0, 7, 4,
            ];
            for (var t = 0; t < segTris.Length; t++)
            {
                tris.Add(b + segTris[t]);
            }
        }
    }

    private static void AppendCone(
        List<Vector3> verts,
        List<Vector3> norms,
        List<Vector3> uvs,
        List<Vector4> cols,
        List<uint> tris,
        Vector3 baseCenter,
        Vector3 tip,
        Vector3 axisDir,
        float radius,
        Vector4 color)
    {
        const int sides = 10;
        Vector3 helper = MathF.Abs(Vector3.Dot(axisDir, Vector3.UnitY)) > 0.92f ? Vector3.UnitX : Vector3.UnitY;
        Vector3 ortho1 = Vector3.Normalize(Vector3.Cross(helper, axisDir));
        Vector3 ortho2 = Vector3.Normalize(Vector3.Cross(axisDir, ortho1));

        uint baseCenterIdx = (uint)verts.Count;
        verts.Add(baseCenter);
        norms.Add(-axisDir);
        uvs.Add(Vector3.Zero);
        cols.Add(color);

        uint tipIdx = (uint)verts.Count;
        verts.Add(tip);
        norms.Add(axisDir);
        uvs.Add(Vector3.Zero);
        cols.Add(color);

        uint ringStart = (uint)verts.Count;
        for (var i = 0; i < sides; i++)
        {
            float angle = (i / (float)sides) * MathF.Tau;
            Vector3 radial = (ortho1 * MathF.Cos(angle)) + (ortho2 * MathF.Sin(angle));
            verts.Add(baseCenter + (radial * radius));
            norms.Add(radial);
            uvs.Add(Vector3.Zero);
            cols.Add(color);
        }

        for (var i = 0; i < sides; i++)
        {
            uint curr = ringStart + (uint)i;
            uint next = ringStart + (uint)((i + 1) % sides);

            tris.Add(curr);
            tris.Add(next);
            tris.Add(tipIdx);

            tris.Add(baseCenterIdx);
            tris.Add(next);
            tris.Add(curr);
        }
    }

    private static void AppendAxisCube(
        List<Vector3> verts,
        List<Vector3> norms,
        List<Vector3> uvs,
        List<Vector4> cols,
        List<uint> tris,
        Vector3 center,
        Vector3 axisX,
        Vector3 axisY,
        Vector3 axisZ,
        float halfExtent,
        Vector4 color)
    {
        Vector3 ex = axisX * halfExtent;
        Vector3 ey = axisY * halfExtent;
        Vector3 ez = axisZ * halfExtent;

        uint b = (uint)verts.Count;
        verts.Add(center - ex - ey - ez); // 0
        verts.Add(center + ex - ey - ez); // 1
        verts.Add(center + ex + ey - ez); // 2
        verts.Add(center - ex + ey - ez); // 3
        verts.Add(center - ex - ey + ez); // 4
        verts.Add(center + ex - ey + ez); // 5
        verts.Add(center + ex + ey + ez); // 6
        verts.Add(center - ex + ey + ez); // 7

        for (var i = 0; i < 8; i++)
        {
            norms.Add(Vector3.UnitY);
            uvs.Add(Vector3.Zero);
            cols.Add(color);
        }

        uint[] boxTris =
        [
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            1, 2, 6, 1, 6, 5,
            0, 4, 7, 0, 7, 3,
            3, 7, 6, 3, 6, 2,
            0, 1, 5, 0, 5, 4,
        ];
        for (var i = 0; i < boxTris.Length; i++)
        {
            tris.Add(b + boxTris[i]);
        }
    }

    private static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        float lenSq = v.LengthSquared();
        return lenSq > 1e-8f ? v / MathF.Sqrt(lenSq) : fallback;
    }
}
