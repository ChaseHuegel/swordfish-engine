#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.IO;
using Swordfish.Library.IO;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Debug;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Voxels;
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

    [Theory]
    [InlineData(BrickFace.Top)]
    [InlineData(BrickFace.Bottom)]
    [InlineData(BrickFace.Front)]
    [InlineData(BrickFace.Back)]
    [InlineData(BrickFace.Left)]
    [InlineData(BrickFace.Right)]
    public void CubeFaceFilterProducesFourBoundaryEdgesWithNormalOffset(BrickFace face)
    {
        const float normalOffset = 0.0175f;
        var renderer = new RecordingLineRenderer();
        using var outline = new MeshOutline(
            renderer,
            Vector4.One,
            new Cube(),
            outset: 1f,
            faceMask: face.ToMask(),
            normalOffset: normalOffset
        );

        outline.Render(new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));

        Assert.Equal(4, renderer.Lines.Count);
        Vector3 faceNormal = face.ToLocalNormal();
        float expectedPlane = 0.5f + normalOffset;
        Assert.All(renderer.Lines, record =>
        {
            Assert.Equal(expectedPlane, Vector3.Dot(record.Line.Start, faceNormal), precision: 4);
            Assert.Equal(expectedPlane, Vector3.Dot(record.Line.End, faceNormal), precision: 4);
        });
    }

    [Fact]
    public void SlopeRampMaskProducesFourSlopedEdgesOffsetAlongRampNormal()
    {
        const float normalOffset = 0.0175f;
        Mesh slope = LoadBuiltInMesh("slope.obj");
        BrickFaceMask rampMask = PaintInteractionResolver.ExpandFaceMask(BrickFace.Front.ToMask(), BrickShape.Slope);
        Assert.Equal(BrickFaceMask.Front | BrickFaceMask.Top, rampMask);
        Assert.Equal(rampMask, PaintInteractionResolver.ExpandFaceMask(BrickFace.Top.ToMask(), BrickShape.Slope));

        var renderer = new RecordingLineRenderer();
        using var outline = new MeshOutline(
            renderer,
            Vector4.One,
            slope,
            outset: 1f,
            faceMask: rampMask,
            normalOffset: normalOffset
        );

        outline.Render(new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));

        Assert.Equal(4, renderer.Lines.Count);
        Vector3 rampNormal = Vector3.Normalize(new Vector3(0f, 1f, 1f));
        Assert.All(renderer.Lines, record =>
        {
            Assert.Equal(normalOffset, Vector3.Dot(record.Line.Start, rampNormal), precision: 4);
            Assert.Equal(normalOffset, Vector3.Dot(record.Line.End, rampNormal), precision: 4);
        });
    }

    [Theory]
    [InlineData(BrickFace.Left, 3)]
    [InlineData(BrickFace.Right, 3)]
    [InlineData(BrickFace.Back, 4)]
    [InlineData(BrickFace.Bottom, 4)]
    public void SlopeFlatFacesProduceSingleFaceOutlines(BrickFace face, int expectedEdges)
    {
        const float normalOffset = 0.0175f;
        Mesh slope = LoadBuiltInMesh("slope.obj");
        BrickFaceMask mask = PaintInteractionResolver.ExpandFaceMask(face.ToMask(), BrickShape.Slope);

        var renderer = new RecordingLineRenderer();
        using var outline = new MeshOutline(
            renderer,
            Vector4.One,
            slope,
            outset: 1f,
            faceMask: mask,
            normalOffset: normalOffset
        );

        outline.Render(new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));

        Assert.Equal(expectedEdges, renderer.Lines.Count);
        Vector3 faceNormal = face.ToLocalNormal();
        float expectedPlane = 0.5f + normalOffset;
        Assert.All(renderer.Lines, record =>
        {
            Assert.Equal(expectedPlane, Vector3.Dot(record.Line.Start, faceNormal), precision: 4);
            Assert.Equal(expectedPlane, Vector3.Dot(record.Line.End, faceNormal), precision: 4);
        });
    }

    [Fact]
    public void BuiltInShapesOffsetRecessedAndOffCenterFacesOutward()
    {
        const float normalOffset = 0.0175f;

        //  Slab top sits at y = 0; normal offset moves it strictly above the surface.
        var slabRenderer = new RecordingLineRenderer();
        using (var slabTop = new MeshOutline(slabRenderer, Vector4.One, LoadBuiltInMesh("slab.obj"), outset: 1f, faceMask: BrickFaceMask.Top, normalOffset: normalOffset))
        {
            slabTop.Render(new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            Assert.Equal(4, slabRenderer.Lines.Count);
            Assert.All(slabRenderer.Lines, record =>
            {
                Assert.Equal(normalOffset, record.Line.Start.Y, precision: 4);
                Assert.Equal(normalOffset, record.Line.End.Y, precision: 4);
            });
        }

        //  Plate top sits at y = -0.375; normal offset moves it upward (+Y), not downward into the plate.
        var plateRenderer = new RecordingLineRenderer();
        using (var plateTop = new MeshOutline(plateRenderer, Vector4.One, LoadBuiltInMesh("plate.obj"), outset: 1f, faceMask: BrickFaceMask.Top, normalOffset: normalOffset))
        {
            plateTop.Render(new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            Assert.Equal(4, plateRenderer.Lines.Count);
            float expectedY = -0.375f + normalOffset;
            Assert.All(plateRenderer.Lines, record =>
            {
                Assert.Equal(expectedY, record.Line.Start.Y, precision: 4);
                Assert.Equal(expectedY, record.Line.End.Y, precision: 4);
            });
        }

        //  Column right face sits at x = 0.25; normal offset moves it to 0.25 + normalOffset.
        var columnRenderer = new RecordingLineRenderer();
        using (var columnRight = new MeshOutline(columnRenderer, Vector4.One, LoadBuiltInMesh("column.obj"), outset: 1f, faceMask: BrickFaceMask.Right, normalOffset: normalOffset))
        {
            columnRight.Render(new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            Assert.Equal(4, columnRenderer.Lines.Count);
            float expectedX = 0.25f + normalOffset;
            Assert.All(columnRenderer.Lines, record =>
            {
                Assert.Equal(expectedX, record.Line.Start.X, precision: 4);
                Assert.Equal(expectedX, record.Line.End.X, precision: 4);
            });
        }

        //  Stair top has two treads (y = 0.5 and y = 0.0); both offset upward by normalOffset.
        var stairTopRenderer = new RecordingLineRenderer();
        using (var stairTop = new MeshOutline(stairTopRenderer, Vector4.One, LoadBuiltInMesh("stair.obj"), outset: 1f, faceMask: BrickFaceMask.Top, normalOffset: normalOffset))
        {
            stairTop.Render(new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            Assert.Equal(8, stairTopRenderer.Lines.Count);
            Assert.All(stairTopRenderer.Lines, record =>
            {
                bool onUpperTread = MathF.Abs(record.Line.Start.Y - (0.5f + normalOffset)) < 1e-4f
                    && MathF.Abs(record.Line.End.Y - (0.5f + normalOffset)) < 1e-4f;
                bool onLowerTread = MathF.Abs(record.Line.Start.Y - normalOffset) < 1e-4f
                    && MathF.Abs(record.Line.End.Y - normalOffset) < 1e-4f;
                Assert.True(onUpperTread || onLowerTread);
            });
        }

        //  Stair front has two risers (z = 0.5 and z = 0.0); both offset forward (+Z) by normalOffset.
        var stairFrontRenderer = new RecordingLineRenderer();
        using (var stairFront = new MeshOutline(stairFrontRenderer, Vector4.One, LoadBuiltInMesh("stair.obj"), outset: 1f, faceMask: BrickFaceMask.Front, normalOffset: normalOffset))
        {
            stairFront.Render(new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            Assert.Equal(8, stairFrontRenderer.Lines.Count);
            Assert.All(stairFrontRenderer.Lines, record =>
            {
                bool onLowerRiser = MathF.Abs(record.Line.Start.Z - (0.5f + normalOffset)) < 1e-4f
                    && MathF.Abs(record.Line.End.Z - (0.5f + normalOffset)) < 1e-4f;
                bool onUpperRiser = MathF.Abs(record.Line.Start.Z - normalOffset) < 1e-4f
                    && MathF.Abs(record.Line.End.Z - normalOffset) < 1e-4f;
                Assert.True(onLowerRiser || onUpperRiser);
            });
        }
    }

    [Fact]
    public void FaceOutlineRespectsRotatedStructureAndBrickOrientation()
    {
        const float normalOffset = 0.0175f;
        var structureOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f);
        var voxelOrientation = new Orientation(pitch: 1, yaw: 0, roll: 0);
        Quaternion combined = structureOrientation * voxelOrientation.ToQuaternion();
        var brickWorldPos = new Vector3(3f, -2f, 5f);

        Vector3 worldTopNormal = Vector3.Transform(Vector3.UnitY, combined);
        BrickFace resolvedFace = PaintInteractionResolver.WorldNormalToBrickFace(worldTopNormal, structureOrientation, voxelOrientation);
        Assert.Equal(BrickFace.Top, resolvedFace);

        var renderer = new RecordingLineRenderer();
        using var outline = new MeshOutline(
            renderer,
            Vector4.One,
            LoadBuiltInMesh("slab.obj"),
            outset: 1f,
            faceMask: resolvedFace.ToMask(),
            normalOffset: normalOffset
        );

        outline.Render(new TransformComponent(brickWorldPos, combined, Vector3.One));

        Assert.Equal(4, renderer.Lines.Count);
        Assert.All(renderer.Lines, record =>
        {
            Assert.Equal(normalOffset, Vector3.Dot(record.Line.Start - brickWorldPos, worldTopNormal), precision: 4);
            Assert.Equal(normalOffset, Vector3.Dot(record.Line.End - brickWorldPos, worldTopNormal), precision: 4);
        });
    }

    private static Mesh LoadBuiltInMesh(string fileName)
    {
        var path = new PathInfo(Path.Combine("TestFiles", "Meshing", "meshes", fileName));
        return new ObjParser().Parse(path);
    }
}
