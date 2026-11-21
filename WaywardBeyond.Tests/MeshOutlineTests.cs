#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using WaywardBeyond.Client.Debug;
using Xunit;

namespace WaywardBeyond.Tests;

public class MeshOutlineTests
{
    private readonly record struct LineRecord(Line Line, bool AlwaysOnTop);

    private sealed class RecordingLineRenderer : ILineRenderer
    {
        public readonly List<LineRecord> Lines = [];

        public Line CreateLine(bool alwaysOnTop = false)
        {
            return Add(Vector3.Zero, Vector3.Zero, alwaysOnTop);
        }

        public Line CreateLine(Vector3 start, Vector3 end, bool alwaysOnTop = false)
        {
            return Add(start, end, alwaysOnTop);
        }

        public Line CreateLine(Vector3 start, Vector3 end, Vector4 color, bool alwaysOnTop = false)
        {
            return Add(start, end, alwaysOnTop);
        }

        public void DeleteLine(Line line)
        {
            Lines.RemoveAll(record => record.Line == line);
        }

        private Line Add(Vector3 start, Vector3 end, bool alwaysOnTop)
        {
            var line = new Line(this, start, end, Vector4.Zero);
            Lines.Add(new LineRecord(line, alwaysOnTop));
            return line;
        }
    }

    [Fact]
    public void CubeProducesFeatureEdgesAndUsesDepthTestedLines()
    {
        var renderer = new RecordingLineRenderer();
        using var outline = new MeshOutline(renderer, Vector4.One, new Cube());

        Assert.Equal(12, renderer.Lines.Count);
        Assert.All(renderer.Lines, record => Assert.False(record.AlwaysOnTop));

        outline.Visible = true;
        Assert.All(renderer.Lines, record => Assert.Equal(Vector4.One, record.Line.Color));

        outline.Visible = false;
        Assert.All(renderer.Lines, record => Assert.Equal(Vector4.Zero, record.Line.Color));
    }

    [Fact]
    public void RenderAppliesTheOutset()
    {
        var renderer = new RecordingLineRenderer();
        using var outline = new MeshOutline(renderer, Vector4.One, new Cube(), outset: 1.05f);

        outline.Render(new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));

        Assert.All(renderer.Lines, record =>
        {
            Assert.InRange(record.Line.Start.X, -0.5251f, 0.5251f);
            Assert.InRange(record.Line.Start.Y, -0.5251f, 0.5251f);
            Assert.InRange(record.Line.Start.Z, -0.5251f, 0.5251f);
        });
        Assert.Contains(renderer.Lines, record => MathF.Abs(record.Line.Start.X) > 0.52f);
    }

    [Fact]
    public void OpenMeshIncludesBoundaryEdges()
    {
        var renderer = new RecordingLineRenderer();
        var mesh = new Mesh(
            [0, 1, 2],
            [Vector3.Zero, Vector3.UnitX, Vector3.UnitY],
            [Vector4.One, Vector4.One, Vector4.One],
            [Vector3.Zero, Vector3.Zero, Vector3.Zero],
            [Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ]
        );

        using var outline = new MeshOutline(renderer, Vector4.One, mesh);

        Assert.Equal(3, renderer.Lines.Count);
    }
}
