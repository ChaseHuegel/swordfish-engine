using System;
using System.Numerics;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.Graphics;

/// <summary>
/// Shared unit-length dual-cylinder beam mesh along local -Z with outer sheath followed by inner core
/// so back-to-front transparent rendering composites the bright core inside the glowing sheath.
/// </summary>
internal sealed class LaserBeamMesh : Mesh
{
    private const int SEGMENTS = 8;
    private const float SHEATH_START_RADIUS = 0.048f;
    private const float SHEATH_END_RADIUS = 0.056f;
    private const float CORE_START_RADIUS = 0.015f;
    private const float CORE_END_RADIUS = 0.018f;

    /// <summary>Constructs the shared unit-length laser beam mesh.</summary>
    public LaserBeamMesh() : this(BuildGeometry())
    {
    }

    private LaserBeamMesh(GeometryData geometry)
        : base(geometry.Triangles, geometry.Vertices, geometry.Colors, geometry.Uv, geometry.Normals)
    {
        Shared = true;
    }

    private static GeometryData BuildGeometry()
    {
        const int vertsPerLayer = (SEGMENTS + 1) * 2;
        const int totalVerts = vertsPerLayer * 2;
        const int trisPerLayer = SEGMENTS * 2;
        const int totalIndices = trisPerLayer * 3 * 2;

        var vertices = new Vector3[totalVerts];
        var colors = new Vector4[totalVerts];
        var uv = new Vector3[totalVerts];
        var normals = new Vector3[totalVerts];
        var triangles = new uint[totalIndices];

        var vertexCursor = 0;
        var indexCursor = 0;

        //  Emit outer sheath first so the inner core renders on top inside the single draw call.
        AppendCylinderLayer(
            SHEATH_START_RADIUS,
            SHEATH_END_RADIUS,
            layerId: 1f,
            vertices,
            colors,
            uv,
            normals,
            triangles,
            ref vertexCursor,
            ref indexCursor
        );
        AppendCylinderLayer(
            CORE_START_RADIUS,
            CORE_END_RADIUS,
            layerId: 0f,
            vertices,
            colors,
            uv,
            normals,
            triangles,
            ref vertexCursor,
            ref indexCursor
        );

        return new GeometryData(triangles, vertices, colors, uv, normals);
    }

    private static void AppendCylinderLayer(
        float startRadius,
        float endRadius,
        float layerId,
        Vector3[] vertices,
        Vector4[] colors,
        Vector3[] uv,
        Vector3[] normals,
        uint[] triangles,
        ref int vertexCursor,
        ref int indexCursor
    ) {
        int baseVertex = vertexCursor;

        for (var i = 0; i <= SEGMENTS; i++)
        {
            float u = (float)i / SEGMENTS;
            float angle = u * MathF.Tau;
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);
            var normal = new Vector3(cos, sin, 0f);

            vertices[vertexCursor] = new Vector3(cos * startRadius, sin * startRadius, 0f);
            colors[vertexCursor] = Vector4.One;
            uv[vertexCursor] = new Vector3(u, 0f, layerId);
            normals[vertexCursor] = normal;
            vertexCursor++;

            vertices[vertexCursor] = new Vector3(cos * endRadius, sin * endRadius, -1f);
            colors[vertexCursor] = Vector4.One;
            uv[vertexCursor] = new Vector3(u, 1f, layerId);
            normals[vertexCursor] = normal;
            vertexCursor++;
        }

        for (var i = 0; i < SEGMENTS; i++)
        {
            uint v00 = (uint)(baseVertex + i * 2);
            uint v01 = v00 + 1;
            uint v10 = v00 + 2;
            uint v11 = v00 + 3;

            triangles[indexCursor++] = v00;
            triangles[indexCursor++] = v01;
            triangles[indexCursor++] = v11;

            triangles[indexCursor++] = v00;
            triangles[indexCursor++] = v11;
            triangles[indexCursor++] = v10;
        }
    }

    private readonly struct GeometryData(
        uint[] triangles,
        Vector3[] vertices,
        Vector4[] colors,
        Vector3[] uv,
        Vector3[] normals
    ) {
        public readonly uint[] Triangles = triangles;
        public readonly Vector3[] Vertices = vertices;
        public readonly Vector4[] Colors = colors;
        public readonly Vector3[] Uv = uv;
        public readonly Vector3[] Normals = normals;
    }
}
