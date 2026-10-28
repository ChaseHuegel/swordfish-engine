using System;
using System.Numerics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Extensions;
using Swordfish.Settings;
using Xunit;

namespace Swordfish.Tests;

public class ShadowCascadeMathTests
{
    [Fact]
    public void CalculateCascadeSplits_DistributesMonotonically()
    {
        Span<float> splits = stackalloc float[4];
        float near = 0.1f;
        float far = 1000.0f;
        float maxDistance = 100.0f;
        int count = 4;

        ShadowCascadeMath.CalculateCascadeSplits(near, far, maxDistance, count, splits, 0.5f);

        Assert.True(splits[0] > near);
        Assert.True(splits[1] > splits[0]);
        Assert.True(splits[2] > splits[1]);
        Assert.True(splits[3] > splits[2]);
        Assert.Equal(maxDistance, splits[3], precision: 4);
    }

    [Fact]
    public void CalculateCascadeSplits_ClampsToEffectiveFar()
    {
        Span<float> splits = stackalloc float[2];
        float near = 1.0f;
        float far = 50.0f;
        float maxDistance = 20.0f;

        ShadowCascadeMath.CalculateCascadeSplits(near, far, maxDistance, 2, splits, 0.8f);

        Assert.True(splits[0] > near);
        Assert.Equal(maxDistance, splits[1], precision: 4);
    }

    [Fact]
    public void CreateOrthographicOpenGL_ProducesValidProjection()
    {
        float left = -10.0f;
        float right = 10.0f;
        float bottom = -5.0f;
        float top = 5.0f;
        float zNear = 0.0f;
        float zFar = 20.0f;

        Matrix4x4 m = ShadowCascadeMath.CreateOrthographicOpenGL(left, right, bottom, top, zNear, zFar);

        // M11 = 2 / (right - left) = 2 / 20 = 0.1
        Assert.Equal(0.1f, m.M11, precision: 4);
        // M22 = 2 / (top - bottom) = 2 / 10 = 0.2
        Assert.Equal(0.2f, m.M22, precision: 4);
        // M33 = -2 / (zFar - zNear) = -2 / 20 = -0.1
        Assert.Equal(-0.1f, m.M33, precision: 4);
        // M44 = 1.0
        Assert.Equal(1.0f, m.M44, precision: 4);
    }

    [Fact]
    public void CalculateCascadeMatrix_BuildsValidViewAndProj()
    {
        Matrix4x4 cameraView = Matrix4x4.CreateLookAt(new Vector3(0, 2, 5), Vector3.Zero, Vector3.UnitY);
        float fov = MathF.PI / 4.0f;
        float aspect = 16.0f / 9.0f;
        float near = 0.1f;
        float far = 20.0f;
        Vector3 lightDir = Vector3.Normalize(new Vector3(0.5f, -1.0f, 0.3f));
        uint resolution = 2048;

        ShadowCascadeMath.CalculateCascadeMatrix(
            cameraView,
            fov,
            aspect,
            near,
            far,
            lightDir,
            resolution,
            out Matrix4x4 cascadeView,
            out Matrix4x4 cascadeProj);

        Assert.NotEqual(Matrix4x4.Identity, cascadeView);
        Assert.NotEqual(Matrix4x4.Identity, cascadeProj);

        bool invertible = Matrix4x4.Invert(cascadeView, out _);
        Assert.True(invertible);
    }

    [Fact]
    public void ShadowSettings_DefaultValues_AreCorrect()
    {
        var settings = new ShadowSettings();

        Assert.True(settings.Enabled.Get());
        Assert.Equal(1024, settings.Resolution.Get());
        Assert.Equal(3, settings.CascadeCount.Get());
        Assert.Equal(2, settings.MaxPointShadows.Get());
        Assert.Equal(ShadowFilterMode.Soft, settings.FilterMode.Get());
        Assert.Equal(96f, settings.MaxDistance.Get());
    }

    [Fact]
    public void CreatePerspectiveOpenGL_MapsDepthCorrectly()
    {
        float near = 0.1f;
        float far = 100f;
        Matrix4x4 proj = ShadowCascadeMath.CreatePerspectiveOpenGL((float)Math.PI * 0.5f, 1.0f, near, far);

        // Point at near plane along -Z (view space)
        Vector4 nearPt = Vector4.Transform(new Vector4(0f, 0f, -near, 1f), proj);
        float nearNdc = nearPt.Z / nearPt.W;
        Assert.Equal(-1.0f, nearNdc, precision: 4);

        // Point at far plane along -Z (view space)
        Vector4 farPt = Vector4.Transform(new Vector4(0f, 0f, -far, 1f), proj);
        float farNdc = farPt.Z / farPt.W;
        Assert.Equal(1.0f, farNdc, precision: 4);

        // Viewport mapping (0.5 * ndc + 0.5) matches (far / (far - near)) - ((far * near) / ((far - near) * d))
        float midDist = 10f;
        Vector4 midPt = Vector4.Transform(new Vector4(0f, 0f, -midDist, 1f), proj);
        float midNdc = midPt.Z / midPt.W;
        float depthWin = (0.5f * midNdc) + 0.5f;

        float expectedDepth = (far / (far - near)) - ((far * near) / ((far - near) * midDist));
        Assert.Equal(expectedDepth, depthWin, precision: 4);
    }

    [Fact]
    public void GetCameraPosition_And_GetCameraForward_AreCorrect()
    {
        var eye = new Vector3(15f, 25f, -35f);
        var target = new Vector3(15f, 25f, 10f);
        Matrix4x4 view = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY);

        Vector3 camPos = view.GetCameraPosition();
        Assert.Equal(eye.X, camPos.X, precision: 4);
        Assert.Equal(eye.Y, camPos.Y, precision: 4);
        Assert.Equal(eye.Z, camPos.Z, precision: 4);

        Vector3 camForward = view.GetCameraForward();
        Assert.Equal(0f, camForward.X, precision: 4);
        Assert.Equal(0f, camForward.Y, precision: 4);
        Assert.Equal(1f, camForward.Z, precision: 4);

        // Tilt up
        var tiltTarget = eye + Vector3.Normalize(new Vector3(0f, 1f, 1f));
        Matrix4x4 tiltedView = Matrix4x4.CreateLookAt(eye, tiltTarget, Vector3.UnitY);

        Vector3 tiltedCamPos = tiltedView.GetCameraPosition();
        Assert.Equal(eye.X, tiltedCamPos.X, precision: 4);
        Assert.Equal(eye.Y, tiltedCamPos.Y, precision: 4);
        Assert.Equal(eye.Z, tiltedCamPos.Z, precision: 4);

        Vector3 expectedForward = Vector3.Normalize(new Vector3(0f, 1f, 1f));
        Vector3 tiltedForward = tiltedView.GetCameraForward();
        Assert.Equal(expectedForward.X, tiltedForward.X, precision: 4);
        Assert.Equal(expectedForward.Y, tiltedForward.Y, precision: 4);
        Assert.Equal(expectedForward.Z, tiltedForward.Z, precision: 4);
    }
}
