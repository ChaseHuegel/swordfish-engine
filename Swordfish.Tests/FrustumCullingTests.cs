using System;
using System.Numerics;
using Swordfish.Graphics;
using Xunit;

namespace Swordfish.Tests;

public class FrustumCullingTests
{
    [Fact]
    public void BoundingBox_FromVertices_CalculatesCorrectCenterExtentsAndRadius()
    {
        Vector3[] vertices =
        [
            new(0f, 0f, 0f),
            new(2f, 4f, 6f),
            new(2f, 0f, 0f),
            new(0f, 4f, 6f),
        ];

        BoundingBox bounds = BoundingBox.FromVertices(vertices);

        Assert.Equal(new Vector3(1f, 2f, 3f), bounds.Center);
        Assert.Equal(new Vector3(1f, 2f, 3f), bounds.Extents);

        float expectedRadius = MathF.Sqrt(1f + 4f + 9f);
        Assert.Equal(expectedRadius, bounds.SphereRadius, precision: 4);
    }

    [Fact]
    public void BoundingBox_EmptyVertices_ReturnsDefault()
    {
        BoundingBox bounds = BoundingBox.FromVertices([]);

        Assert.Equal(Vector3.Zero, bounds.Center);
        Assert.Equal(Vector3.Zero, bounds.Extents);
        Assert.Equal(0f, bounds.SphereRadius);
    }

    [Fact]
    public void Mesh_Constructor_InitializesBounds()
    {
        Vector3[] vertices =
        [
            new(-1f, -1f, -1f),
            new(1f, 1f, 1f),
        ];

        var mesh = new Mesh([], vertices, [], [], []);

        Assert.Equal(Vector3.Zero, mesh.Bounds.Center);
        Assert.Equal(Vector3.One, mesh.Bounds.Extents);
        Assert.Equal(MathF.Sqrt(3f), mesh.Bounds.SphereRadius, precision: 4);
    }

    [Fact]
    public void Frustum_ExtractsPlanes_AndCullsOutsideAABB()
    {
        Matrix4x4 view = Matrix4x4.CreateLookAt(
            cameraPosition: new Vector3(0f, 0f, 10f),
            cameraTarget: new Vector3(0f, 0f, 0f),
            cameraUpVector: Vector3.UnitY
        );

        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
            fieldOfView: MathF.PI / 3f, // 60 degrees
            aspectRatio: 1f,
            nearPlaneDistance: 0.1f,
            farPlaneDistance: 100f
        );

        Frustum frustum = Frustum.FromViewProjection(view * projection);

        // Center target in view
        Assert.True(frustum.Intersects(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1f)));

        // Behind camera
        Assert.False(frustum.Intersects(new Vector3(0f, 0f, 20f), new Vector3(1f, 1f, 1f)));

        // Far to the left
        Assert.False(frustum.Intersects(new Vector3(-50f, 0f, 0f), new Vector3(1f, 1f, 1f)));

        // Beyond far plane
        Assert.False(frustum.Intersects(new Vector3(0f, 0f, -150f), new Vector3(1f, 1f, 1f)));

        // Partially intersecting left frustum edge
        Assert.True(frustum.Intersects(new Vector3(-6f, 0f, 0f), new Vector3(2f, 2f, 2f)));
    }

    [Fact]
    public void Frustum_IntersectsSphere_CullsCorrectly()
    {
        Matrix4x4 view = Matrix4x4.CreateLookAt(
            cameraPosition: new Vector3(0f, 0f, 10f),
            cameraTarget: new Vector3(0f, 0f, 0f),
            cameraUpVector: Vector3.UnitY
        );

        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
            fieldOfView: MathF.PI / 3f,
            aspectRatio: 1f,
            nearPlaneDistance: 0.1f,
            farPlaneDistance: 100f
        );

        Frustum frustum = Frustum.FromViewProjection(view * projection);

        Assert.True(frustum.IntersectsSphere(new Vector3(0f, 0f, 0f), radius: 1f));
        Assert.False(frustum.IntersectsSphere(new Vector3(0f, 0f, 20f), radius: 1f));
        Assert.False(frustum.IntersectsSphere(new Vector3(-50f, 0f, 0f), radius: 1f));
    }

    [Fact]
    public void Mesh_ConstructorWithBoundsSkipsTheVertexScan()
    {
        var bounds = new BoundingBox(new Vector3(5f, 6f, 7f), Vector3.One, 1f);
        var mesh = new Mesh([], [], [], [], [], bounds);

        Assert.Equal(bounds, mesh.Bounds);
    }

    [Fact]
    public void BoundingBox_Transform_AppliesTranslationRotationAndScale()
    {
        var bounds = new BoundingBox(Vector3.Zero, Vector3.One, MathF.Sqrt(3f));
        Matrix4x4 matrix = Matrix4x4.CreateRotationY(MathF.PI / 2f) * Matrix4x4.CreateScale(2f) * Matrix4x4.CreateTranslation(5f, 0f, 0f);

        BoundingBox transformed = bounds.Transform(matrix);

        Assert.Equal(new Vector3(5f, 0f, 0f), transformed.Center);
        Assert.Equal(new Vector3(2f, 2f, 2f), transformed.Extents);
    }

    [Fact]
    public void BoundingBox_Union_ContainsBothBoxes()
    {
        var left = new BoundingBox(new Vector3(0f, 0f, 0f), Vector3.One, 0f);
        var right = new BoundingBox(new Vector3(4f, 0f, 0f), Vector3.One, 0f);

        BoundingBox union = BoundingBox.Union(left, right);

        Assert.Equal(new Vector3(2f, 0f, 0f), union.Center);
        Assert.Equal(new Vector3(3f, 1f, 1f), union.Extents);
    }

    [Fact]
    public void Frustum_RejectsAnOffCameraChunkAfterWorldTransform()
    {
        Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0f, 0f, 10f), Vector3.Zero, Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, 1f, 0.1f, 100f);
        Frustum frustum = Frustum.FromViewProjection(view * projection);

        var behind = new BoundingBox(new Vector3(0f, 0f, 20f), Vector3.One, 1f);
        var visible = new BoundingBox(Vector3.Zero, Vector3.One, 1f);

        Assert.False(frustum.Intersects(behind.Center, behind.Extents));
        Assert.True(frustum.Intersects(visible.Center, visible.Extents));
    }
}
