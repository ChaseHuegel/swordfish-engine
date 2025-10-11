using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.Debug;

using LineTemplate = (Vector3 Start, Vector3 End);

public sealed class CubeGizmo : IDisposable
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

    public CubeGizmo(ILineRenderer lineRenderer, Vector4 color)
    {
        _color = color;
        
        _lines = new Line[12];
        for (var i = 0; i < _lines.Length; i++)
        {
            _lines[i] = lineRenderer.CreateLine(alwaysOnTop: false);
            _lines[i].Color = color;
        }

        _templates = new LineTemplate[12];
        //  Bottom, rear
        _templates[0].Start = new Vector3(-0.5f, -0.5f, -0.5f);
        _templates[0].End = new Vector3(0.5f, -0.5f, -0.5f);
        //  Bottom, right
        _templates[1].Start = new Vector3(0.5f, -0.5f, -0.5f);
        _templates[1].End = new Vector3(0.5f, -0.5f, 0.5f);
        //  Bottom, forward
        _templates[2].Start = new Vector3(0.5f, -0.5f, 0.5f);
        _templates[2].End = new Vector3(-0.5f, -0.5f, 0.5f);
        //  Bottom, left
        _templates[3].Start = new Vector3(-0.5f, -0.5f, 0.5f);
        _templates[3].End = new Vector3(-0.5f, -0.5f, -0.5f);
        
        //  Top, rear
        _templates[4].Start = new Vector3(-0.5f, 0.5f, -0.5f);
        _templates[4].End = new Vector3(0.5f, 0.5f, -0.5f);
        //  Top, right
        _templates[5].Start = new Vector3(0.5f, 0.5f, -0.5f);
        _templates[5].End = new Vector3(0.5f, 0.5f, 0.5f);
        //  Top, forward
        _templates[6].Start = new Vector3(0.5f, 0.5f, 0.5f);
        _templates[6].End = new Vector3(-0.5f, 0.5f, 0.5f);
        //  Top, left
        _templates[7].Start = new Vector3(-0.5f, 0.5f, 0.5f);
        _templates[7].End = new Vector3(-0.5f, 0.5f, -0.5f);
        
        //  Vertical, rear left
        _templates[8].Start = new Vector3(-0.5f, -0.5f, -0.5f);
        _templates[8].End = new Vector3(-0.5f, 0.5f, -0.5f);
        //  Vertical, rear right
        _templates[9].Start = new Vector3(0.5f, -0.5f, -0.5f);
        _templates[9].End = new Vector3(0.5f, 0.5f, -0.5f);
        
        //  Vertical, forward left
        _templates[10].Start = new Vector3(-0.5f, -0.5f, 0.5f);
        _templates[10].End = new Vector3(-0.5f, 0.5f, 0.5f);
        //  Vertical, forward right
        _templates[11].Start = new Vector3(0.5f, -0.5f, 0.5f);
        _templates[11].End = new Vector3(0.5f, 0.5f, 0.5f);
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
        float scale = 1.02f + (float)MathS.PingPong(time: _time * 0.0625d, max: 0.04d);
        
        for (var i = 0; i < _lines.Length; i++)
        {
            Line line = _lines[i];
            line.Start = transform.Position + Vector3.Transform(_templates[i].Start * scale, transform.Orientation);
            line.End = transform.Position + Vector3.Transform(_templates[i].End * scale, transform.Orientation);
        }
    }
}