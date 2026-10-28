using System.Numerics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

/// <summary>Calculates split distances and projection matrices for cascaded shadow mapping.</summary>
internal static class ShadowCascadeMath
{
    /// <summary>Creates an orthographic projection matrix using OpenGL clip volume conventions.</summary>
    public static Matrix4x4 CreateOrthographicOpenGL(float left, float right, float bottom, float top, float zNear, float zFar)
    {
        float invRightLeft = 1.0f / (right - left);
        float invTopBottom = 1.0f / (top - bottom);
        float invFarNear = 1.0f / (zFar - zNear);

        var m = Matrix4x4.Identity;
        m.M11 = 2.0f * invRightLeft;
        m.M22 = 2.0f * invTopBottom;
        m.M33 = -2.0f * invFarNear;
        m.M41 = -(right + left) * invRightLeft;
        m.M42 = -(top + bottom) * invTopBottom;
        m.M43 = -(zFar + zNear) * invFarNear;
        return m;
    }

    /// <summary>Creates a perspective projection matrix using OpenGL clip volume conventions.</summary>
    public static Matrix4x4 CreatePerspectiveOpenGL(float fovRadians, float aspectRatio, float zNear, float zFar)
    {
        float tanHalfFov = MathF.Tan(fovRadians * 0.5f);
        float invFarNear = 1.0f / (zFar - zNear);

        var m = default(Matrix4x4);
        m.M11 = 1.0f / (tanHalfFov * aspectRatio);
        m.M22 = 1.0f / tanHalfFov;
        m.M33 = -(zFar + zNear) * invFarNear;
        m.M34 = -1.0f;
        m.M43 = -(2.0f * zFar * zNear) * invFarNear;
        return m;
    }

    /// <summary>Computes log-linear cascade split distances between the near and effective far planes.</summary>
    public static void CalculateCascadeSplits(float nearPlane, float farPlane, float maxDistance, int cascadeCount, Span<float> splits, float lambda = 0.8f)
    {
        float effectiveFar = MathF.Min(farPlane, maxDistance);
        if (effectiveFar <= nearPlane)
        {
            effectiveFar = nearPlane + 10f;
        }

        for (var i = 1; i <= cascadeCount && i - 1 < splits.Length; i++)
        {
            float p = (float)i / cascadeCount;
            float cLog = nearPlane * MathF.Pow(effectiveFar / nearPlane, p);
            float cUniform = nearPlane + (effectiveFar - nearPlane) * p;
            splits[i - 1] = (lambda * cLog) + ((1.0f - lambda) * cUniform);
        }
    }

    /// <summary>Calculates the world-space bounding sphere of a camera frustum slice.</summary>
    public static void CalculateCascadeSphere(
        Matrix4x4 cameraView,
        float fovRadians,
        float aspectRatio,
        float cascadeNear,
        float cascadeFar,
        out Vector3 center,
        out float radius
    ) {
        if (!Matrix4x4.Invert(cameraView, out Matrix4x4 invCameraView))
        {
            invCameraView = Matrix4x4.Identity;
        }

        float tanHalfFov = MathF.Tan(fovRadians * 0.5f);
        float nearH = cascadeNear * tanHalfFov;
        float nearW = nearH * aspectRatio;
        float farH = cascadeFar * tanHalfFov;
        float farW = farH * aspectRatio;

        // View-space frustum corners (camera looks along -Z)
        Span<Vector3> frustumCorners = stackalloc Vector3[8]
        {
            new(-nearW,  nearH, -cascadeNear),
            new( nearW,  nearH, -cascadeNear),
            new(-nearW, -nearH, -cascadeNear),
            new( nearW, -nearH, -cascadeNear),
            new(-farW,   farH, -cascadeFar),
            new( farW,   farH, -cascadeFar),
            new(-farW,  -farH, -cascadeFar),
            new( farW,  -farH, -cascadeFar),
        };

        center = Vector3.Zero;
        for (var i = 0; i < 8; i++)
        {
            frustumCorners[i] = Vector3.Transform(frustumCorners[i], invCameraView);
            center += frustumCorners[i];
        }
        center /= 8.0f;

        var maxDistSq = 0.0f;
        for (var i = 0; i < 8; i++)
        {
            float distSq = Vector3.DistanceSquared(frustumCorners[i], center);
            if (distSq > maxDistSq)
            {
                maxDistSq = distSq;
            }
        }
        radius = MathF.Ceiling(MathF.Sqrt(maxDistSq) * 16.0f) / 16.0f;
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y) || !float.IsFinite(center.Z))
        {
            center = Vector3.Zero;
        }

        if (!float.IsFinite(radius) || radius <= 0.0f)
        {
            radius = 1.0f;
        }
    }

    /// <summary>Calculates a texel-snapped orthographic light view-projection matrix for a cascade frustum slice.</summary>
    public static void CalculateCascadeMatrix(
        Matrix4x4 cameraView,
        float fovRadians,
        float aspectRatio,
        float cascadeNear,
        float cascadeFar,
        Vector3 lightDirection,
        uint resolution,
        out Matrix4x4 cascadeView,
        out Matrix4x4 cascadeProj
    ) {
        CalculateCascadeSphere(
            cameraView,
            fovRadians,
            aspectRatio,
            cascadeNear,
            cascadeFar,
            out Vector3 center,
            out float radius
        );

        Vector3 normLightDir = lightDirection.LengthSquared() > 1e-8f
            ? Vector3.Normalize(lightDirection)
            : -Vector3.UnitY;
        if (!float.IsFinite(normLightDir.X) || !float.IsFinite(normLightDir.Y) || !float.IsFinite(normLightDir.Z))
        {
            normLightDir = -Vector3.UnitY;
        }

        Vector3 up = MathF.Abs(normLightDir.Y) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;
        Vector3 right = Vector3.Normalize(Vector3.Cross(normLightDir, up));
        if (!float.IsFinite(right.X) || !float.IsFinite(right.Y) || !float.IsFinite(right.Z) || right.LengthSquared() < 1e-8f)
        {
            right = Vector3.UnitX;
        }

        up = Vector3.Normalize(Vector3.Cross(right, normLightDir));
        if (!float.IsFinite(up.X) || !float.IsFinite(up.Y) || !float.IsFinite(up.Z) || up.LengthSquared() < 1e-8f)
        {
            up = Vector3.UnitY;
        }

        //  Snap the cascade center on the light-space texel grid. The light view
        //  must use the snapped center, or the shadow grid moves with the camera.
        float resolutionF = Math.Max(1f, resolution);
        float texelSize = (2.0f * radius) / Math.Max(1f, resolutionF - 2.0f);
        if (!float.IsFinite(texelSize) || texelSize <= 0.0f)
        {
            texelSize = 0.01f;
        }

        float paddedRadius = radius + texelSize;
        float centerRight = Vector3.Dot(center, right);
        float centerUp = Vector3.Dot(center, up);
        float snappedRight = MathF.Round(centerRight / texelSize) * texelSize;
        float snappedUp = MathF.Round(centerUp / texelSize) * texelSize;
        Vector3 snappedCenter = center
            + ((snappedRight - centerRight) * right)
            + ((snappedUp - centerUp) * up);
        if (!float.IsFinite(snappedCenter.X) || !float.IsFinite(snappedCenter.Y) || !float.IsFinite(snappedCenter.Z))
        {
            snappedCenter = center;
        }

        float lightDistance = paddedRadius * 2.0f;
        Vector3 eye = snappedCenter - (normLightDir * lightDistance);
        cascadeView = Matrix4x4.CreateLookAt(eye, snappedCenter, up);

        float minX = -paddedRadius;
        float maxX = paddedRadius;
        float minY = -paddedRadius;
        float maxY = paddedRadius;
        float minZ = 0.0f;
        float maxZ = paddedRadius * 4.0f;

        cascadeProj = CreateOrthographicOpenGL(minX, maxX, minY, maxY, minZ, maxZ);
    }
}
