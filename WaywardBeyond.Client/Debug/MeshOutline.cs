using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;

namespace WaywardBeyond.Client.Debug;

using LineTemplate = (Vector3 Start, Vector3 End);

/// <summary>Draws the feature edges of a mesh as an outline.</summary>
internal sealed class MeshOutline : IDisposable
{
    private const float FEATURE_COSINE = 0.9f;
    private const float POSITION_SCALE = 10000f;

    private readonly Line[] _lines;
    private readonly LineTemplate[] _templates;
    private readonly Vector4 _color;
    private readonly float _outset;
    private bool _visible;

    /// <summary>Gets or sets whether the outline is visible.</summary>
    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value)
            {
                return;
            }

            for (var i = 0; i < _lines.Length; i++)
            {
                _lines[i].Color = value ? _color : Vector4.Zero;
            }

            _visible = value;
        }
    }

    /// <summary>Creates an outline for the mesh.</summary>
    public MeshOutline(
        ILineRenderer lineRenderer,
        Vector4 color,
        Mesh mesh,
        float outset = 1.035f,
        bool alwaysOnTop = false
    ) {
        _color = color;
        _outset = outset;

        List<LineTemplate> edges = BuildFeatureEdges(mesh);
        _lines = new Line[edges.Count];
        _templates = new LineTemplate[edges.Count];

        for (var i = 0; i < edges.Count; i++)
        {
            _lines[i] = lineRenderer.CreateLine(alwaysOnTop: alwaysOnTop);
            _lines[i].Color = Vector4.Zero;
            _templates[i] = edges[i];
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        for (var i = 0; i < _lines.Length; i++)
        {
            _lines[i].Dispose();
        }
    }

    /// <summary>Updates the outline for the transform.</summary>
    public void Render(TransformComponent transform)
    {
        Vector3 scale = transform.Scale * _outset;

        for (var i = 0; i < _lines.Length; i++)
        {
            Line line = _lines[i];
            line.Start = transform.Position + Vector3.Transform(_templates[i].Start * scale, transform.Orientation);
            line.End = transform.Position + Vector3.Transform(_templates[i].End * scale, transform.Orientation);
        }
    }

    private static List<LineTemplate> BuildFeatureEdges(Mesh mesh)
    {
        var edges = new Dictionary<EdgeKey, EdgeData>();

        for (var i = 0; i + 2 < mesh.Triangles.Length; i += 3)
        {
            Vector3 a = mesh.Vertices[mesh.Triangles[i]];
            Vector3 b = mesh.Vertices[mesh.Triangles[i + 1]];
            Vector3 c = mesh.Vertices[mesh.Triangles[i + 2]];

            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.LengthSquared() < 1e-12f)
            {
                continue;
            }

            normal = Vector3.Normalize(normal);
            AddEdge(edges, a, b, normal);
            AddEdge(edges, b, c, normal);
            AddEdge(edges, c, a, normal);
        }

        var templates = new List<LineTemplate>(edges.Count);
        foreach (EdgeData data in edges.Values)
        {
            if (data.Feature || data.Count == 1)
            {
                templates.Add(new LineTemplate(data.A, data.B));
            }
        }

        return templates;
    }

    private static void AddEdge(Dictionary<EdgeKey, EdgeData> edges, Vector3 a, Vector3 b, Vector3 normal)
    {
        var key = new EdgeKey(a, b);
        if (!edges.TryGetValue(key, out EdgeData data))
        {
            edges[key] = new EdgeData
            {
                A = a,
                B = b,
                Normal = normal,
                Count = 1,
            };
            return;
        }

        if (Vector3.Dot(data.Normal, normal) < FEATURE_COSINE)
        {
            data.Feature = true;
        }

        data.Count++;
        edges[key] = data;
    }

    private readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        private readonly int _ax;
        private readonly int _ay;
        private readonly int _az;
        private readonly int _bx;
        private readonly int _by;
        private readonly int _bz;

        public EdgeKey(Vector3 a, Vector3 b)
        {
            int ax = Quantize(a.X);
            int ay = Quantize(a.Y);
            int az = Quantize(a.Z);
            int bx = Quantize(b.X);
            int by = Quantize(b.Y);
            int bz = Quantize(b.Z);

            if (Compare(ax, ay, az, bx, by, bz) <= 0)
            {
                _ax = ax;
                _ay = ay;
                _az = az;
                _bx = bx;
                _by = by;
                _bz = bz;
            }
            else
            {
                _ax = bx;
                _ay = by;
                _az = bz;
                _bx = ax;
                _by = ay;
                _bz = az;
            }
        }

        public bool Equals(EdgeKey other)
        {
            return _ax == other._ax
                && _ay == other._ay
                && _az == other._az
                && _bx == other._bx
                && _by == other._by
                && _bz == other._bz;
        }

        public override bool Equals(object? obj)
        {
            return obj is EdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_ax, _ay, _az, _bx, _by, _bz);
        }

        private static int Quantize(float value)
        {
            return (int)MathF.Round(value * POSITION_SCALE);
        }

        private static int Compare(int ax, int ay, int az, int bx, int by, int bz)
        {
            int result = ax.CompareTo(bx);
            if (result != 0)
            {
                return result;
            }

            result = ay.CompareTo(by);
            if (result != 0)
            {
                return result;
            }

            return az.CompareTo(bz);
        }
    }

    private struct EdgeData
    {
        public Vector3 A;
        public Vector3 B;
        public Vector3 Normal;
        public int Count;
        public bool Feature;
    }
}
