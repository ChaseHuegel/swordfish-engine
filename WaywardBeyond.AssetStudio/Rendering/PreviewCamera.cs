using System;
using System.Numerics;

namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Manages orbit angles, first-person framing, raycasting, and OpenGL view/projection matrices.</summary>
public sealed class PreviewCamera
{
    private const float MIN_PITCH_DEGREES = -85f;
    private const float MAX_PITCH_DEGREES = 85f;
    private const float MIN_DISTANCE = 0.6f;
    private const float EDITOR_MIN_DISTANCE = 0.35f;
    private const float MAX_DISTANCE = 160f;
    private const float DEGREES_TO_RADIANS = MathF.PI / 180f;

    /// <summary>Creates a preview camera initialized to the default single-brick orbit preset.</summary>
    public PreviewCamera()
    {
        ApplyPreset(PreviewCameraPreset.OrbitBrick);
    }

    /// <summary>True when locked to the game's first-person eye camera at <c>(0, 0, 0)</c> looking down <c>-Z</c>.</summary>
    public bool IsFirstPerson { get; set; }

    /// <summary>Horizontal orbit angle in degrees around the Y axis.</summary>
    public float YawDegrees { get; set; }

    /// <summary>Vertical elevation angle in degrees above the XZ plane.</summary>
    public float PitchDegrees { get; set; }

    /// <summary>Distance from <see cref="Target"/> to the camera eye position.</summary>
    public float Distance { get; set; }

    /// <summary>World-space point the camera orbits around or looks toward.</summary>
    public Vector3 Target { get; set; }

    /// <summary>Vertical field of view in degrees.</summary>
    public float FovDegrees { get; set; } = 42f;

    /// <summary>Near clip plane distance.</summary>
    public float NearPlane { get; set; } = 0.05f;

    /// <summary>Far clip plane distance.</summary>
    public float FarPlane { get; set; } = 300f;

    /// <summary>Active framing preset restored by <see cref="Reset"/>.</summary>
    public PreviewCameraPreset ActivePreset { get; private set; }

    /// <summary>Configures the camera for first-person view model editing at <paramref name="fovDegrees"/>.</summary>
    public void ConfigureFirstPersonViewModel(float fovDegrees = 90f)
    {
        ActivePreset = PreviewCameraPreset.ItemViewModelFirstPerson;
        IsFirstPerson = true;
        Target = new Vector3(0f, 0f, -1f);
        YawDegrees = 0f;
        PitchDegrees = 0f;
        Distance = 1f;
        FovDegrees = Math.Clamp(fovDegrees, 60f, 120f);
        NearPlane = 0.01f;
        FarPlane = 100f;
    }

    /// <summary>Applies the default framing parameters for <paramref name="preset"/>.</summary>
    public void ApplyPreset(PreviewCameraPreset preset)
    {
        ActivePreset = preset;
        IsFirstPerson = false;
        NearPlane = 0.05f;
        FarPlane = 300f;
        switch (preset)
        {
            case PreviewCameraPreset.BrickShapeGrid:
                Target = Vector3.Zero;
                YawDegrees = 24f;
                PitchDegrees = 22f;
                Distance = 6.8f;
                FovDegrees = 40f;
                break;
            case PreviewCameraPreset.ConnectedShowcase:
                Target = Vector3.Zero;
                YawDegrees = 20f;
                PitchDegrees = 18f;
                Distance = 6.8f;
                FovDegrees = 40f;
                break;
            case PreviewCameraPreset.ItemViewModel:
                Target = new Vector3(0f, -0.05f, 0f);
                YawDegrees = -38f;
                PitchDegrees = 14f;
                Distance = 1.9f;
                FovDegrees = 48f;
                break;
            case PreviewCameraPreset.ItemWorldModel:
                Target = Vector3.Zero;
                YawDegrees = 45f;
                PitchDegrees = 28f;
                Distance = 2.6f;
                FovDegrees = 40f;
                break;
            case PreviewCameraPreset.Blueprint:
                Target = Vector3.Zero;
                YawDegrees = 42f;
                PitchDegrees = 28f;
                Distance = 6.0f;
                FovDegrees = 42f;
                break;
            case PreviewCameraPreset.ItemViewModelFirstPerson:
                ConfigureFirstPersonViewModel(90f);
                break;
            case PreviewCameraPreset.ItemWorldModelEditor:
                Target = new Vector3(0f, 0.82f, -0.35f);
                YawDegrees = 32f;
                PitchDegrees = 16f;
                Distance = 3.55f;
                FovDegrees = 45f;
                NearPlane = 0.02f;
                FarPlane = 150f;
                break;
            case PreviewCameraPreset.OrbitBrick:
            default:
                Target = Vector3.Zero;
                YawDegrees = 42f;
                PitchDegrees = 28f;
                Distance = 2.95f;
                FovDegrees = 40f;
                break;
        }
    }

    /// <summary>Restores the camera orientation and distance for <see cref="ActivePreset"/>.</summary>
    public void Reset()
    {
        ApplyPreset(ActivePreset);
    }

    /// <summary>Applies incremental yaw and pitch orbit deltas in degrees when in orbit mode.</summary>
    public void Orbit(float deltaYawDegrees, float deltaPitchDegrees)
    {
        if (IsFirstPerson)
        {
            return;
        }

        YawDegrees = (YawDegrees + deltaYawDegrees) % 360f;
        PitchDegrees = Math.Clamp(PitchDegrees + deltaPitchDegrees, MIN_PITCH_DEGREES, MAX_PITCH_DEGREES);
    }

    /// <summary>Translates <see cref="Target"/> along the camera right and up axes scaled by <see cref="Distance"/>.</summary>
    public void Pan(float deltaX, float deltaY)
    {
        if (IsFirstPerson)
        {
            return;
        }

        float yawRad = YawDegrees * DEGREES_TO_RADIANS;
        float pitchRad = PitchDegrees * DEGREES_TO_RADIANS;
        float cosPitch = MathF.Cos(pitchRad);

        var forward = new Vector3(
            -MathF.Sin(yawRad) * cosPitch,
            -MathF.Sin(pitchRad),
            -MathF.Cos(yawRad) * cosPitch);
        Vector3 right = Vector3.Cross(forward, Vector3.UnitY);
        float rightLenSq = right.LengthSquared();
        right = rightLenSq > 1e-6f ? right / MathF.Sqrt(rightLenSq) : Vector3.UnitX;

        Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));
        float scale = Distance * 0.002f;
        Target += ((-right * deltaX) + (up * deltaY)) * scale;
    }

    /// <summary>Zooms the orbit camera in or out using a scroll wheel delta.</summary>
    public void Zoom(float wheelDelta)
    {
        if (IsFirstPerson)
        {
            return;
        }

        float minDistance = ActivePreset == PreviewCameraPreset.ItemWorldModelEditor ? EDITOR_MIN_DISTANCE : MIN_DISTANCE;
        float clampedDelta = Math.Clamp(wheelDelta, -12f, 12f);
        float factor = MathF.Pow(0.88f, clampedDelta);
        Distance = Math.Clamp(Distance * factor, minDistance, MAX_DISTANCE);
        NearPlane = MathF.Max(ActivePreset == PreviewCameraPreset.ItemWorldModelEditor ? 0.01f : 0.05f, Distance * 0.01f);
        FarPlane = MathF.Max(150f, Distance * 12f);
    }

    /// <summary>Centers the target and adjusts distance to fit the axis-aligned bounds tightly in view.</summary>
    public void FrameBounds(in Vector3 min, in Vector3 max, float paddingFactor = 1.25f)
    {
        if (IsFirstPerson)
        {
            return;
        }

        Target = (min + max) * 0.5f;
        float radius = MathF.Max(Vector3.Distance(min, max) * 0.5f, 0.5f);
        float halfFovRad = (FovDegrees * 0.5f) * DEGREES_TO_RADIANS;
        float fitDistance = (radius / MathF.Sin(halfFovRad)) * paddingFactor;
        Distance = Math.Clamp(fitDistance, MIN_DISTANCE, MAX_DISTANCE);
        NearPlane = MathF.Max(0.05f, Distance * 0.01f);
        FarPlane = MathF.Max(100f, Distance * 12f);
    }

    /// <summary>Computes the world-space eye position for the current orbit or first-person camera.</summary>
    public Vector3 GetEyePosition()
    {
        if (IsFirstPerson)
        {
            return Vector3.Zero;
        }

        float yawRad = YawDegrees * DEGREES_TO_RADIANS;
        float pitchRad = PitchDegrees * DEGREES_TO_RADIANS;
        float cosPitch = MathF.Cos(pitchRad);

        var offset = new Vector3(
            MathF.Sin(yawRad) * cosPitch,
            MathF.Sin(pitchRad),
            MathF.Cos(yawRad) * cosPitch
        ) * Distance;

        return Target + offset;
    }

    /// <summary>Computes the right-handed look-at view matrix from the current camera state.</summary>
    public Matrix4x4 GetViewMatrix()
    {
        if (IsFirstPerson)
        {
            return Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY);
        }

        return Matrix4x4.CreateLookAt(GetEyePosition(), Target, Vector3.UnitY);
    }

    /// <summary>Computes a right-handed OpenGL [-1, 1] NDC perspective projection matrix.</summary>
    public Matrix4x4 GetProjectionMatrix(float aspect)
    {
        float safeAspect = MathF.Max(aspect, 0.05f);
        float fovRad = Math.Clamp(FovDegrees, 10f, 120f) * DEGREES_TO_RADIANS;
        float f = 1f / MathF.Tan(fovRad * 0.5f);
        float invNearFar = 1f / (NearPlane - FarPlane);

        return new Matrix4x4(
            f / safeAspect, 0f, 0f, 0f,
            0f, f, 0f, 0f,
            0f, 0f, (FarPlane + NearPlane) * invNearFar, -1f,
            0f, 0f, (2f * FarPlane * NearPlane) * invNearFar, 0f
        );
    }

    /// <summary>Constructs a world-space picking ray originating at the near plane under <paramref name="screenX"/>, <paramref name="screenY"/>.</summary>
    public (Vector3 Origin, Vector3 Direction) ScreenPointToRay(
        float screenX,
        float screenY,
        float viewportWidth,
        float viewportHeight)
    {
        float safeW = MathF.Max(viewportWidth, 1f);
        float safeH = MathF.Max(viewportHeight, 1f);
        float aspect = safeW / safeH;

        float ndcX = ((2f * screenX) / safeW) - 1f;
        float ndcY = 1f - ((2f * screenY) / safeH);

        Matrix4x4 viewProj = GetViewMatrix() * GetProjectionMatrix(aspect);
        if (!Matrix4x4.Invert(viewProj, out Matrix4x4 invViewProj))
        {
            Vector3 eye = GetEyePosition();
            return (eye, Vector3.Normalize(Target - eye));
        }

        Vector4 nearClip = Vector4.Transform(new Vector4(ndcX, ndcY, -1f, 1f), invViewProj);
        Vector4 farClip = Vector4.Transform(new Vector4(ndcX, ndcY, 1f, 1f), invViewProj);

        Vector3 nearPos = MathF.Abs(nearClip.W) > 1e-6f
            ? new Vector3(nearClip.X, nearClip.Y, nearClip.Z) / nearClip.W
            : GetEyePosition();
        Vector3 farPos = MathF.Abs(farClip.W) > 1e-6f
            ? new Vector3(farClip.X, farClip.Y, farClip.Z) / farClip.W
            : Target;

        Vector3 delta = farPos - nearPos;
        float lenSq = delta.LengthSquared();
        Vector3 dir = lenSq > 1e-8f ? delta / MathF.Sqrt(lenSq) : -Vector3.UnitZ;
        return (nearPos, dir);
    }

    /// <summary>Projects <paramref name="worldPos"/> into viewport pixel coordinates, returning false when behind the eye.</summary>
    public bool TryProjectToScreen(
        Vector3 worldPos,
        float viewportWidth,
        float viewportHeight,
        out Vector2 screenPos,
        out float ndcDepth)
    {
        float safeW = MathF.Max(viewportWidth, 1f);
        float safeH = MathF.Max(viewportHeight, 1f);
        Matrix4x4 viewProj = GetViewMatrix() * GetProjectionMatrix(safeW / safeH);
        Vector4 clip = Vector4.Transform(new Vector4(worldPos, 1f), viewProj);

        if (clip.W <= 1e-4f)
        {
            screenPos = default;
            ndcDepth = -1f;
            return false;
        }

        float invW = 1f / clip.W;
        float ndcX = clip.X * invW;
        float ndcY = clip.Y * invW;
        ndcDepth = clip.Z * invW;

        screenPos = new Vector2(
            (ndcX * 0.5f + 0.5f) * safeW,
            (0.5f - ndcY * 0.5f) * safeH);
        return true;
    }
}
