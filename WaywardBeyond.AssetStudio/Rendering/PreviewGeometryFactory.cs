using System;
using System.Collections.Generic;
using System.Numerics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Meshing;

namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Builds procedural 3D preview meshes with OpenGL-aligned UVs and outward normals.</summary>
public static class PreviewGeometryFactory
{
    /// <summary>Creates a centered textured 3D preview cube with 0..1 UVs on every face and CCW outward winding.</summary>
    public static VoxelMesh CreateTexturedCube(float size = 1f, bool applySubtleFaceShading = true)
    {
        float half = size > 0f ? size * 0.5f : 0.5f;

        var vertices = new Vector3[24];
        var normals = new Vector3[24];
        var uv = new Vector3[24];
        var colors = new Vector4[24];
        var triangles = new uint[36];

        // Winding and UVs match CubeMeshBuilder (v=0 bottom, v=1 top; CCW outward).
        WriteFace(
            faceIndex: 0,
            new Vector3(-half, half, half),
            new Vector3(half, half, half),
            new Vector3(half, half, -half),
            new Vector3(-half, half, -half),
            new Vector3(0f, 1f, 0f),
            applySubtleFaceShading ? 1.00f : 1f,
            vertices, normals, uv, colors, triangles);

        WriteFace(
            faceIndex: 1,
            new Vector3(-half, -half, -half),
            new Vector3(half, -half, -half),
            new Vector3(half, -half, half),
            new Vector3(-half, -half, half),
            new Vector3(0f, -1f, 0f),
            applySubtleFaceShading ? 0.80f : 1f,
            vertices, normals, uv, colors, triangles);

        WriteFace(
            faceIndex: 2,
            new Vector3(-half, -half, half),
            new Vector3(half, -half, half),
            new Vector3(half, half, half),
            new Vector3(-half, half, half),
            new Vector3(0f, 0f, 1f),
            applySubtleFaceShading ? 0.96f : 1f,
            vertices, normals, uv, colors, triangles);

        WriteFace(
            faceIndex: 3,
            new Vector3(half, -half, -half),
            new Vector3(-half, -half, -half),
            new Vector3(-half, half, -half),
            new Vector3(half, half, -half),
            new Vector3(0f, 0f, -1f),
            applySubtleFaceShading ? 0.92f : 1f,
            vertices, normals, uv, colors, triangles);

        WriteFace(
            faceIndex: 4,
            new Vector3(half, -half, half),
            new Vector3(half, -half, -half),
            new Vector3(half, half, -half),
            new Vector3(half, half, half),
            new Vector3(1f, 0f, 0f),
            applySubtleFaceShading ? 0.88f : 1f,
            vertices, normals, uv, colors, triangles);

        WriteFace(
            faceIndex: 5,
            new Vector3(-half, -half, -half),
            new Vector3(-half, -half, half),
            new Vector3(-half, half, half),
            new Vector3(-half, half, -half),
            new Vector3(-1f, 0f, 0f),
            applySubtleFaceShading ? 0.88f : 1f,
            vertices, normals, uv, colors, triangles);

        return new VoxelMesh(triangles, vertices, colors, uv, normals);
    }

    /// <summary>Creates a smooth UV sphere with outward CCW winding, consistent seam normals, and 0..1 UVs.</summary>
    public static VoxelMesh CreateTexturedSphere(
        int latitudeSegments = 28,
        int longitudeSegments = 40,
        float radius = 0.62f)
    {
        int latSegs = Math.Max(3, latitudeSegments);
        int lonSegs = Math.Max(3, longitudeSegments);
        float r = radius > 0f && float.IsFinite(radius) ? radius : 0.62f;

        int ringStride = lonSegs + 1;
        int vertexCount = (latSegs + 1) * ringStride;
        var vertices = new Vector3[vertexCount];
        var normals = new Vector3[vertexCount];
        var uv = new Vector3[vertexCount];
        var colors = new Vector4[vertexCount];

        for (var lat = 0; lat <= latSegs; lat++)
        {
            float v = 1f - ((float)lat / latSegs);
            float theta = ((float)lat / latSegs) * MathF.PI;
            float sinTheta = lat == 0 || lat == latSegs ? 0f : MathF.Sin(theta);
            float cosTheta = lat == 0 ? 1f : (lat == latSegs ? -1f : MathF.Cos(theta));

            for (var lon = 0; lon <= lonSegs; lon++)
            {
                float u = (float)lon / lonSegs;
                bool isSeam = lon == 0 || lon == lonSegs;
                float phi = isSeam ? 0f : u * MathF.Tau;
                float sinPhi = isSeam ? 0f : MathF.Sin(phi);
                float cosPhi = isSeam ? 1f : MathF.Cos(phi);

                var normal = new Vector3(
                    -sinTheta * sinPhi,
                    cosTheta,
                    -sinTheta * cosPhi);
                normal = Vector3.Normalize(normal);

                int idx = (lat * ringStride) + lon;
                vertices[idx] = normal * r;
                normals[idx] = normal;
                uv[idx] = new Vector3(u, v, 0f);
                colors[idx] = Vector4.One;
            }
        }

        var triangles = new List<uint>((latSegs - 1) * lonSegs * 6);
        for (var lat = 0; lat < latSegs; lat++)
        {
            for (var lon = 0; lon < lonSegs; lon++)
            {
                uint a = (uint)((lat * ringStride) + lon);
                uint b = a + 1;
                uint c = (uint)(((lat + 1) * ringStride) + lon);
                uint d = c + 1;

                if (lat != latSegs - 1)
                {
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(d);
                }

                if (lat != 0)
                {
                    triangles.Add(a);
                    triangles.Add(d);
                    triangles.Add(b);
                }
            }
        }

        return new VoxelMesh([.. triangles], vertices, colors, uv, normals);
    }

    /// <summary>Creates a centered upright textured quad facing +Z with 0..1 UVs and CCW winding.</summary>
    public static VoxelMesh CreateTexturedPlane(float size = 1.15f)
    {
        float half = size > 0f && float.IsFinite(size) ? size * 0.5f : 0.575f;

        Vector3[] vertices =
        [
            new Vector3(-half, -half, 0f),
            new Vector3(half, -half, 0f),
            new Vector3(half, half, 0f),
            new Vector3(-half, half, 0f),
        ];
        Vector3[] normals =
        [
            Vector3.UnitZ,
            Vector3.UnitZ,
            Vector3.UnitZ,
            Vector3.UnitZ,
        ];
        Vector3[] uv =
        [
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(0f, 1f, 0f),
        ];
        Vector4[] colors =
        [
            Vector4.One,
            Vector4.One,
            Vector4.One,
            Vector4.One,
        ];
        uint[] triangles = [0, 1, 2, 0, 2, 3];

        return new VoxelMesh(triangles, vertices, colors, uv, normals);
    }

    /// <summary>Creates a 1.0 m x 1.0 m floor reference plane centered at <c>(0, 0, 0)</c> with 0.25 m subdivisions and RGB origin axes.</summary>
    public static IReadOnlyList<PreviewSceneMesh> CreateWorldModelFloorPlane(float sizeMeters = 1.0f)
    {
        float size = sizeMeters > 0f ? sizeMeters : 1.0f;
        float half = size * 0.5f;

        // 1. Translucent dark slate surface on Y = 0
        Vector3[] floorVerts =
        [
            new Vector3(-half, 0f, half),
            new Vector3(half, 0f, half),
            new Vector3(half, 0f, -half),
            new Vector3(-half, 0f, -half),
        ];
        Vector3[] floorNorms = [Vector3.UnitY, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY];
        Vector3[] floorUv = [Vector3.Zero, Vector3.UnitX, new Vector3(1f, 1f, 0f), Vector3.UnitY];
        var fillColor = new Vector4(0.14f, 0.18f, 0.25f, 0.56f);
        Vector4[] floorColors = [fillColor, fillColor, fillColor, fillColor];
        uint[] floorTris = [0, 1, 2, 0, 2, 3];
        var floorMesh = new VoxelMesh(floorTris, floorVerts, floorColors, floorUv, floorNorms);

        // 2. Crisp grid lines (0.25 m pitch) + 1.0 m perimeter border + subtle RGB origin axis markers
        var verts = new List<Vector3>(128);
        var norms = new List<Vector3>(128);
        var uvs = new List<Vector3>(128);
        var cols = new List<Vector4>(128);
        var tris = new List<uint>(192);

        var minorColor = new Vector4(0.32f, 0.39f, 0.50f, 1f);
        var majorColor = new Vector4(0.56f, 0.67f, 0.84f, 1f);
        float yGrid = 0.0015f;

        for (var i = -2; i <= 2; i++)
        {
            float coord = i * (half * 0.5f);
            bool isBorder = Math.Abs(i) == 2;
            float lineHalf = isBorder ? 0.008f : 0.004f;
            Vector4 lineColor = isBorder ? majorColor : minorColor;

            // X-parallel line at Z = coord
            AppendBoxLine(verts, norms, uvs, cols, tris,
                new Vector3(-half, yGrid, coord),
                new Vector3(half, yGrid, coord),
                lineHalf,
                lineHalf * 0.5f,
                lineColor);

            // Z-parallel line at X = coord
            AppendBoxLine(verts, norms, uvs, cols, tris,
                new Vector3(coord, yGrid, -half),
                new Vector3(coord, yGrid, half),
                lineHalf,
                lineHalf * 0.5f,
                lineColor);
        }

        // Subtle origin axes anchored cleanly at (0, 0.003, 0)
        float axisLen = size * 0.30f;
        var origin = new Vector3(0f, 0.004f, 0f);
        AppendBoxLine(verts, norms, uvs, cols, tris, origin, origin + (Vector3.UnitX * axisLen), 0.007f, 0.007f, new Vector4(0.94f, 0.27f, 0.27f, 1f));
        AppendBoxLine(verts, norms, uvs, cols, tris, origin, origin + (Vector3.UnitY * axisLen), 0.007f, 0.007f, new Vector4(0.13f, 0.77f, 0.37f, 1f));
        AppendBoxLine(verts, norms, uvs, cols, tris, origin, origin + (Vector3.UnitZ * axisLen), 0.007f, 0.007f, new Vector4(0.23f, 0.51f, 0.96f, 1f));

        var linesMesh = new VoxelMesh([.. tris], [.. verts], [.. cols], [.. uvs], [.. norms]);

        return
        [
            new PreviewSceneMesh(
                linesMesh,
                Matrix4x4.Identity,
                BrickAlpha.Opaque,
                0f,
                doubleSided: true,
                textureMode: PreviewTextureMode.VertexColorOnly),
            new PreviewSceneMesh(
                floorMesh,
                Matrix4x4.Identity,
                BrickAlpha.Blend,
                0f,
                doubleSided: true,
                textureMode: PreviewTextureMode.VertexColorOnly),
        ];
    }

    /// <summary>Creates a vertical double-sided sprite plane standing on <c>(0, 0, -1)</c> at human scale (<c>1.8 m</c>).</summary>
    public static PreviewSceneMesh CreateBodyReferencePlane(
        float aspectWidthOverHeight,
        float heightMeters = ModelTransformMath.PLAYER_STANDING_HEIGHT_METERS,
        Vector3? centerBase = null)
    {
        float height = heightMeters > 0f ? heightMeters : ModelTransformMath.PLAYER_STANDING_HEIGHT_METERS;
        float aspect = Math.Clamp(float.IsFinite(aspectWidthOverHeight) ? aspectWidthOverHeight : 0.6f, 0.15f, 4.0f);
        float halfW = (height * aspect) * 0.5f;
        Vector3 anchor = centerBase ?? new Vector3(0f, 0f, -1f);

        Vector3[] vertices =
        [
            new Vector3(anchor.X - halfW, anchor.Y, anchor.Z),
            new Vector3(anchor.X + halfW, anchor.Y, anchor.Z),
            new Vector3(anchor.X + halfW, anchor.Y + height, anchor.Z),
            new Vector3(anchor.X - halfW, anchor.Y + height, anchor.Z),
        ];
        Vector3[] normals = [Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ];
        // Bottom-up UV origin matches StandaloneTextureLoader.VerticalFlip().
        Vector3[] uv =
        [
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(0f, 1f, 0f),
        ];
        var tint = new Vector4(0.95f, 0.96f, 0.98f, 1f);
        Vector4[] colors = [tint, tint, tint, tint];
        uint[] triangles = [0, 1, 2, 0, 2, 3];

        var mesh = new VoxelMesh(triangles, vertices, colors, uv, normals);
        return new PreviewSceneMesh(
            mesh,
            Matrix4x4.Identity,
            BrickAlpha.Masked,
            alphaCutoff: 0.5f,
            doubleSided: true,
            textureMode: PreviewTextureMode.ReferenceTexture);
    }

    internal static void AppendBoxLine(
        List<Vector3> verts,
        List<Vector3> norms,
        List<Vector3> uvs,
        List<Vector4> cols,
        List<uint> tris,
        Vector3 start,
        Vector3 end,
        float halfWidth,
        float halfThickness,
        Vector4 color)
    {
        Vector3 axis = end - start;
        float lenSq = axis.LengthSquared();
        if (lenSq < 1e-10f)
        {
            return;
        }

        Vector3 dir = axis / MathF.Sqrt(lenSq);
        Vector3 helper = MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.92f ? Vector3.UnitX : Vector3.UnitY;
        Vector3 right = Vector3.Normalize(Vector3.Cross(helper, dir)) * halfWidth;
        Vector3 up = Vector3.Normalize(Vector3.Cross(dir, right)) * halfThickness;

        uint baseIdx = (uint)verts.Count;
        verts.Add(start - right - up); // 0
        verts.Add(start + right - up); // 1
        verts.Add(start + right + up); // 2
        verts.Add(start - right + up); // 3
        verts.Add(end - right - up);   // 4
        verts.Add(end + right - up);   // 5
        verts.Add(end + right + up);   // 6
        verts.Add(end - right + up);   // 7

        for (var i = 0; i < 8; i++)
        {
            norms.Add(Vector3.UnitY);
            uvs.Add(Vector3.Zero);
            cols.Add(color);
        }

        // Top, bottom, sides, and end caps
        uint[] boxTris =
        [
            3, 2, 6, 3, 6, 7,
            0, 5, 1, 0, 4, 5,
            1, 5, 6, 1, 6, 2,
            0, 3, 7, 0, 7, 4,
            0, 1, 2, 0, 2, 3,
            4, 6, 5, 4, 7, 6,
        ];
        for (var i = 0; i < boxTris.Length; i++)
        {
            tris.Add(baseIdx + boxTris[i]);
        }
    }

    private static void WriteFace(
        int faceIndex,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        Vector3 v3,
        Vector3 normal,
        float brightness,
        Vector3[] vertices,
        Vector3[] normals,
        Vector3[] uv,
        Vector4[] colors,
        uint[] triangles)
    {
        int vBase = faceIndex * 4;
        int tBase = faceIndex * 6;

        vertices[vBase + 0] = v0;
        vertices[vBase + 1] = v1;
        vertices[vBase + 2] = v2;
        vertices[vBase + 3] = v3;

        normals[vBase + 0] = normal;
        normals[vBase + 1] = normal;
        normals[vBase + 2] = normal;
        normals[vBase + 3] = normal;

        uv[vBase + 0] = new Vector3(0f, 0f, 0f);
        uv[vBase + 1] = new Vector3(1f, 0f, 0f);
        uv[vBase + 2] = new Vector3(1f, 1f, 0f);
        uv[vBase + 3] = new Vector3(0f, 1f, 0f);

        var tint = new Vector4(brightness, brightness, brightness, 1f);
        colors[vBase + 0] = tint;
        colors[vBase + 1] = tint;
        colors[vBase + 2] = tint;
        colors[vBase + 3] = tint;

        uint start = (uint)vBase;
        triangles[tBase + 0] = start + 0;
        triangles[tBase + 1] = start + 1;
        triangles[tBase + 2] = start + 2;
        triangles[tBase + 3] = start + 0;
        triangles[tBase + 4] = start + 2;
        triangles[tBase + 5] = start + 3;
    }
}
