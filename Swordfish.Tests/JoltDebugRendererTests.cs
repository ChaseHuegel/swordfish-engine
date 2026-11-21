using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using JoltPhysicsSharp;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.Jolt;
using Swordfish.Library.Types.Shapes;
using Swordfish.Physics;
using Swordfish.Physics.Jolt;
using Xunit;
using BodyType = Swordfish.Physics.BodyType;
using PhysicsSettings = Swordfish.Settings.PhysicsSettings;

namespace Swordfish.Tests;

public class JoltDebugRendererTests
{
    [Fact]
    public void DebugVertexIsSixteenBytesWithPackedRgbaColor()
    {
        Assert.Equal(16, Unsafe.SizeOf<JoltDebugVertex>());

        var color = new JoltColor(0x11, 0x22, 0x33, 0x44);
        var vertex = new JoltDebugVertex(new Vector3(1f, 2f, 3f), color.PackedValue);

        Assert.Equal(new Vector3(1f, 2f, 3f), vertex.Position);
        Assert.Equal(0x44332211u, vertex.PackedColor);
    }

    [Fact]
    public void BatchAccumulatesLinesAndTrianglesContiguously()
    {
        var batch = new JoltDebugBatch();
        Assert.True(batch.IsEmpty);
        Assert.Equal(0, batch.LineVertexCount);
        Assert.Equal(0, batch.TriangleVertexCount);

        batch.AddLine(new Vector3(0f, 0f, 0f), new Vector3(1f, 2f, 3f), 0xFF0000FFu);
        batch.AddLine(new Vector3(4f, 5f, 6f), new Vector3(7f, 8f, 9f), 0xFF00FF00u);
        batch.AddTriangle(new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f), 0x80FFFFFFu);

        Assert.False(batch.IsEmpty);
        Assert.Equal(4, batch.LineVertexCount);
        Assert.Equal(3, batch.TriangleVertexCount);

        ReadOnlySpan<JoltDebugVertex> lines = batch.LineVertices;
        Assert.Equal(new Vector3(0f, 0f, 0f), lines[0].Position);
        Assert.Equal(0xFF0000FFu, lines[0].PackedColor);
        Assert.Equal(new Vector3(1f, 2f, 3f), lines[1].Position);
        Assert.Equal(0xFF0000FFu, lines[1].PackedColor);
        Assert.Equal(new Vector3(4f, 5f, 6f), lines[2].Position);
        Assert.Equal(0xFF00FF00u, lines[2].PackedColor);
        Assert.Equal(new Vector3(7f, 8f, 9f), lines[3].Position);
        Assert.Equal(0xFF00FF00u, lines[3].PackedColor);

        ReadOnlySpan<JoltDebugVertex> triangles = batch.TriangleVertices;
        Assert.Equal(new Vector3(1f, 0f, 0f), triangles[0].Position);
        Assert.Equal(new Vector3(0f, 1f, 0f), triangles[1].Position);
        Assert.Equal(new Vector3(0f, 0f, 1f), triangles[2].Position);
        Assert.Equal(0x80FFFFFFu, triangles[0].PackedColor);
        Assert.Equal(0x80FFFFFFu, triangles[2].PackedColor);
    }

    [Fact]
    public void BatchGrowsPastInitialCapacityAndClearsWithoutLosingDataIntegrity()
    {
        var batch = new JoltDebugBatch();
        const int LINE_COUNT = 5_000;

        for (var i = 0; i < LINE_COUNT; i++)
        {
            batch.AddLine(new Vector3(i, 0f, 0f), new Vector3(i, 1f, 0f), (uint)i);
        }

        Assert.Equal(LINE_COUNT * 2, batch.LineVertexCount);
        Assert.Equal(new Vector3(LINE_COUNT - 1, 1f, 0f), batch.LineVertices[^1].Position);
        Assert.Equal((uint)(LINE_COUNT - 1), batch.LineVertices[^1].PackedColor);

        batch.Clear();
        Assert.True(batch.IsEmpty);
        Assert.Equal(0, batch.LineVertexCount);
        Assert.Equal(0, batch.TriangleVertexCount);
        Assert.True(batch.LineVertices.IsEmpty);
        Assert.True(batch.TriangleVertices.IsEmpty);
    }

    [Fact]
    public void FrustumFilterAcceptsNullFrustumAndNonFiniteBounds()
    {
        Assert.True(FrustumBodyDrawFilter.ShouldDrawBounds(null, new Vector3(-1f), new Vector3(1f)));

        Frustum frustum = CreateLookForwardFrustum();
        Assert.True(FrustumBodyDrawFilter.ShouldDrawBounds(frustum, new Vector3(float.NegativeInfinity), new Vector3(float.PositiveInfinity)));
    }

    [Fact]
    public void FrustumFilterCullsOutsideBoundsAndKeepsVisibleBounds()
    {
        Frustum frustum = CreateLookForwardFrustum();

        //  In front of the camera at (0, 0, -10)
        Assert.True(FrustumBodyDrawFilter.ShouldDrawBounds(frustum, new Vector3(-1f, -1f, -11f), new Vector3(1f, 1f, -9f)));

        //  Behind the camera at (0, 0, +10)
        Assert.False(FrustumBodyDrawFilter.ShouldDrawBounds(frustum, new Vector3(-1f, -1f, 9f), new Vector3(1f, 1f, 11f)));

        //  Far to the right of the frustum at (200, 0, -10)
        Assert.False(FrustumBodyDrawFilter.ShouldDrawBounds(frustum, new Vector3(199f, -1f, -11f), new Vector3(201f, 1f, -9f)));
    }

    [Fact]
    public void DrawBodiesWithFrustumFilterSkipsBodiesOutsideFrustum()
    {
        var settings = new PhysicsSettings();
        var physics = new JoltPhysicsSystem(NullLogger.Instance, settings);
        physics.SetGravity(Vector3.Zero);

        var store = new DataStore();
        AddBoxBody(store, new Vector3(0f, 0f, -10f));
        AddBoxBody(store, new Vector3(0f, 0f, 50f));
        AddBoxBody(store, new Vector3(200f, 0f, -10f));

        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        var drawSettings = new DrawSettings
        {
            DrawShapeWireframe = true,
            DrawVelocity = false,
        };

        using var unfilteredRenderer = new RecordingDebugRenderer();
        physics.DrawBodies(drawSettings, unfilteredRenderer);
        int unfilteredVertices = unfilteredRenderer.Batch.LineVertexCount + unfilteredRenderer.Batch.TriangleVertexCount;

        using var filteredRenderer = new RecordingDebugRenderer();
        using var filter = new FrustumBodyDrawFilter();
        filter.SetFrustum(CreateLookForwardFrustum());
        physics.DrawBodies(drawSettings, filteredRenderer, filter);
        int filteredVertices = filteredRenderer.Batch.LineVertexCount + filteredRenderer.Batch.TriangleVertexCount;

        Assert.True(unfilteredVertices > 0, "Jolt should emit debug geometry for bodies.");
        Assert.True(filteredVertices > 0, "Visible body should still emit debug geometry.");
        Assert.True(filteredVertices < unfilteredVertices, "Out-of-frustum bodies should be culled before geometry generation.");
    }

    private static Frustum CreateLookForwardFrustum()
    {
        Matrix4x4 view = Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2f, 16f / 9f, 0.1f, 100f);
        return Frustum.FromViewProjection(view * projection);
    }

    private static void AddBoxBody(DataStore store, Vector3 position)
    {
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TransformComponent(position, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(entity, new PhysicsComponent(Layers.NON_MOVING, BodyType.Static, CollisionDetection.Discrete));
        store.AddOrUpdate(entity, new ColliderComponent(new Box3(Vector3.One)));
    }

    private sealed class RecordingDebugRenderer : DebugRenderer
    {
        public JoltDebugBatch Batch { get; } = new();

        protected override void DrawLine(Vector3 from, Vector3 to, JoltColor color)
        {
            Batch.AddLine(from, to, color.PackedValue);
        }

        protected override void DrawTriangle(Vector3 v1, Vector3 v2, Vector3 v3, JoltColor color, CastShadow castShadow)
        {
            Batch.AddTriangle(v1, v2, v3, color.PackedValue);
        }

        protected override void DrawText3D(Vector3 position, string? text, JoltColor color, float height = 0.5f)
        {
        }
    }
}
