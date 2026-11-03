using System;
using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
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

    [Fact]
    public void Frustum_KeepsGeometryJustInsideTheNearPlane()
    {
        Matrix4x4 view = Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY);
        Matrix4x4 projection = ShadowCascadeMath.CreatePerspectiveOpenGL(MathF.PI / 3f, 1f, 1f, 100f);
        Frustum frustum = Frustum.FromViewProjection(view * projection);

        //  The near plane sits at 1m, so 1.5m is inside the frustum.
        Assert.True(frustum.Intersects(new Vector3(0f, 0f, -1.5f), new Vector3(0.1f)));

        //  0.5m is in front of the near plane.
        Assert.False(frustum.Intersects(new Vector3(0f, 0f, -0.5f), new Vector3(0.1f)));
    }

    [Fact]
    public void Frustum_KeepsEveryCascadeSliceCorner()
    {
        Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0f, 2f, 5f), Vector3.Zero, Vector3.UnitY);
        float fov = MathF.PI / 3f;
        float aspect = 16f / 9f;
        float near = 0.1f;
        float far = 20f;
        Vector3 lightDirection = Vector3.Normalize(new Vector3(-0.35f, -0.85f, -0.4f));

        ShadowCascadeMath.CalculateCascadeMatrix(
            view, fov, aspect, near, far, lightDirection, resolution: 1024,
            out Matrix4x4 cascadeView, out Matrix4x4 cascadeProj
        );

        Frustum frustum = Frustum.FromViewProjection(cascadeView * cascadeProj);
        Vector3[] corners = GetSliceCorners(view, fov, aspect, near, far);

        for (var i = 0; i < corners.Length; i++)
        {
            Assert.True(frustum.Intersects(corners[i], Vector3.Zero), $"corner {i} was culled");
        }
    }

    [Fact]
    public void Frustum_KeepsCasterBetweenTheLightAndTheCascade()
    {
        Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0f, 2f, 5f), Vector3.Zero, Vector3.UnitY);
        float fov = MathF.PI / 3f;
        float aspect = 16f / 9f;
        float near = 0.1f;
        float far = 20f;
        Vector3 lightDirection = Vector3.Normalize(new Vector3(-0.35f, -0.85f, -0.4f));

        ShadowCascadeMath.CalculateCascadeMatrix(
            view, fov, aspect, near, far, lightDirection, resolution: 1024,
            out Matrix4x4 cascadeView, out Matrix4x4 cascadeProj
        );

        Frustum frustum = Frustum.FromViewProjection(cascadeView * cascadeProj);
        Vector3[] corners = GetSliceCorners(view, fov, aspect, near, far);
        Vector3 center = Vector3.Zero;
        for (var i = 0; i < corners.Length; i++)
        {
            center += corners[i];
        }

        center /= corners.Length;

        //  A caster toward the light must remain in the shadow map.
        ShadowCascadeMath.CalculateCascadeSphere(
            view, fov, aspect, near, far, out _, out float radius
        );
        Vector3 caster = center - (lightDirection * (radius * 0.5f));

        Assert.True(frustum.Intersects(caster, Vector3.Zero));
    }

    private static Vector3[] GetSliceCorners(Matrix4x4 view, float fov, float aspect, float near, float far)
    {
        Assert.True(Matrix4x4.Invert(view, out Matrix4x4 inverseView));

        float tanHalfFov = MathF.Tan(fov * 0.5f);
        float nearH = near * tanHalfFov;
        float nearW = nearH * aspect;
        float farH = far * tanHalfFov;
        float farW = farH * aspect;

        Vector3[] corners =
        [
            new(-nearW, nearH, -near),
            new(nearW, nearH, -near),
            new(-nearW, -nearH, -near),
            new(nearW, -nearH, -near),
            new(-farW, farH, -far),
            new(farW, farH, -far),
            new(-farW, -farH, -far),
            new(farW, -farH, -far),
        ];

        for (var i = 0; i < corners.Length; i++)
        {
            corners[i] = Vector3.Transform(corners[i], inverseView);
        }

        return corners;
    }
}
