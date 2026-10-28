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

        // Transform corners to world space
        var center = Vector3.Zero;
        for (var i = 0; i < 8; i++)
        {
            frustumCorners[i] = Vector3.Transform(frustumCorners[i], invCameraView);
            center += frustumCorners[i];
        }
        center /= 8.0f;

        // Bounding sphere radius
        var maxDistSq = 0.0f;
        for (var i = 0; i < 8; i++)
        {
            float distSq = Vector3.DistanceSquared(frustumCorners[i], center);
            if (distSq > maxDistSq)
            {
                maxDistSq = distSq;
            }
        }
        float radius = MathF.Ceiling(MathF.Sqrt(maxDistSq) * 16.0f) / 16.0f;

        Vector3 normLightDir = Vector3.Normalize(lightDirection);
        if (normLightDir == Vector3.Zero)
        {
            normLightDir = -Vector3.UnitY;
        }

        Vector3 up = MathF.Abs(normLightDir.Y) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;
        Vector3 eye = center - (normLightDir * radius * 2.0f);
        cascadeView = Matrix4x4.CreateLookAt(eye, center, up);

        // Derive AABB in light space
        Vector3 centerLight = Vector3.Transform(center, cascadeView);
        float minX = centerLight.X - radius;
        float maxX = centerLight.X + radius;
        float minY = centerLight.Y - radius;
        float maxY = centerLight.Y + radius;
        float minZ = 0.0f;
        float maxZ = radius * 4.0f;

        // Texel snapping
        float worldUnitsPerTexel = (2.0f * radius) / Math.Max(1u, resolution);
        minX = MathF.Floor(minX / worldUnitsPerTexel) * worldUnitsPerTexel;
        maxX = MathF.Floor(maxX / worldUnitsPerTexel) * worldUnitsPerTexel;
        minY = MathF.Floor(minY / worldUnitsPerTexel) * worldUnitsPerTexel;
        maxY = MathF.Floor(maxY / worldUnitsPerTexel) * worldUnitsPerTexel;

        cascadeProj = CreateOrthographicOpenGL(minX, maxX, minY, maxY, minZ, maxZ);
    }
}
