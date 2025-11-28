using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.Debug;

using LineTemplate = (Vector3 Start, Vector3 End);

public sealed class MeshGizmo : IDisposable
{
    private readonly Line[] _lines;
    private readonly LineTemplate[] _templates;
    private readonly Vector4 _color;
    private bool _visible;
    private double _time;

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

    public MeshGizmo(ILineRenderer lineRenderer, Vector4 color, Mesh mesh)
    {
        _color = color;

        _lines = new Line[mesh.Triangles.Length];
        for (var i = 0; i < _lines.Length; i++)
        {
            _lines[i] = lineRenderer.CreateLine(alwaysOnTop: false);
            _lines[i].Color = color;
        }
        
        _templates = new LineTemplate[mesh.Triangles.Length];
        for (var i = 1; i < mesh.Triangles.Length; i++)
        {
            _templates[i].Start = mesh.Vertices[mesh.Triangles[i - 1]];
            _templates[i].End = mesh.Vertices[mesh.Triangles[i]];
        }
    }

    public void Dispose()
    {
        for (var i = 0; i < _lines.Length; i++)
        {
            _lines[i].Dispose();
        }
    }

    public void Render(double delta, TransformComponent transform)
    {
        _time += delta;
        float scale = 0.95f + (float)MathS.PingPong(time: _time * 0.0625d, max: 0.04d);
        
        for (var i = 0; i < _lines.Length; i++)
        {
            Line line = _lines[i];
            line.Start = transform.Position + Vector3.Transform(_templates[i].Start * scale, transform.Orientation);
            line.End = transform.Position + Vector3.Transform(_templates[i].End * scale, transform.Orientation);
        }
    }
}