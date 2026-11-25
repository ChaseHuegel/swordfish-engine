using System;
using System.Globalization;
using System.Numerics;

namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Transform, Euler decomposition, and vector formatting math matching the game's view model transforms.</summary>
public static class ModelTransformMath
{
    private const float DEGREES_TO_RADIANS = MathF.PI / 180f;
    private const float RADIANS_TO_DEGREES = 180f / MathF.PI;

    /// <summary>Default first-person view model translation matching <c>PlayerViewModelSystem</c>.</summary>
    public static readonly Vector3 DefaultViewModelPosition = new(0.4f, -0.4f, -0.5f);

    /// <summary>Default first-person view model rotation in degrees matching <c>PlayerViewModelSystem</c>.</summary>
    public static readonly Vector3 DefaultViewModelRotation = new(0f, -60f, 0f);

    /// <summary>Default first-person view model scale matching <c>PlayerViewModelSystem</c>.</summary>
    public static readonly Vector3 DefaultViewModelScale = new(0.25f, 0.25f, 0.25f);

    /// <summary>Default world model translation matching <c>ModelDefinition</c>.</summary>
    public static readonly Vector3 DefaultWorldModelPosition = Vector3.Zero;

    /// <summary>Default world model rotation in degrees matching <c>ModelDefinition</c>.</summary>
    public static readonly Vector3 DefaultWorldModelRotation = Vector3.Zero;

    /// <summary>Default world model scale matching <c>ModelDefinition</c>.</summary>
    public static readonly Vector3 DefaultWorldModelScale = Vector3.One;

    /// <summary>Standing human character height in meters matching <c>CharacterDefaults.PLAYER_STANDING_HEIGHT</c>.</summary>
    public const float PLAYER_STANDING_HEIGHT_METERS = 1.8f;

    /// <summary>Creates a rotation matrix from Euler degrees using game Yaw(Y)-Pitch(X)-Roll(Z) convention.</summary>
    public static Matrix4x4 CreateRotationMatrix(Vector3 rotationDegrees)
    {
        Quaternion q = Quaternion.CreateFromYawPitchRoll(
            rotationDegrees.Y * DEGREES_TO_RADIANS,
            rotationDegrees.X * DEGREES_TO_RADIANS,
            rotationDegrees.Z * DEGREES_TO_RADIANS);
        return Matrix4x4.CreateFromQuaternion(q);
    }

    /// <summary>Creates the row-vector world model matrix <c>Scale * YawPitchRoll(Y,X,Z) * Translation</c>.</summary>
    public static Matrix4x4 CreateModelMatrix(Vector3 position, Vector3 rotationDegrees, Vector3 scale)
    {
        return Matrix4x4.CreateScale(scale)
            * CreateRotationMatrix(rotationDegrees)
            * Matrix4x4.CreateTranslation(position);
    }

    /// <summary>Extracts Euler degrees <c>(Pitch=X, Yaw=Y, Roll=Z)</c> from a pure rotation matrix matching <see cref="CreateRotationMatrix"/>.</summary>
    public static Vector3 ExtractYawPitchRollDegrees(in Matrix4x4 rotationMatrix)
    {
        float sinPitch = Math.Clamp(-rotationMatrix.M32, -1f, 1f);
        float pitchRad = MathF.Asin(sinPitch);
        float cosPitch = MathF.Cos(pitchRad);

        float yawRad;
        float rollRad;
        if (MathF.Abs(cosPitch) > 1e-4f)
        {
            yawRad = MathF.Atan2(rotationMatrix.M31, rotationMatrix.M33);
            rollRad = MathF.Atan2(rotationMatrix.M12, rotationMatrix.M22);
        }
        else
        {
            rollRad = 0f;
            yawRad = MathF.Atan2(-rotationMatrix.M13, rotationMatrix.M11);
        }

        return new Vector3(
            NormalizeAngleDegrees(pitchRad * RADIANS_TO_DEGREES),
            NormalizeAngleDegrees(yawRad * RADIANS_TO_DEGREES),
            NormalizeAngleDegrees(rollRad * RADIANS_TO_DEGREES));
    }

    /// <summary>Normalizes an angle in degrees into <c>(-180, 180]</c> and zeroes near-zero negative values.</summary>
    public static float NormalizeAngleDegrees(float degrees)
    {
        if (!float.IsFinite(degrees))
        {
            return 0f;
        }

        float wrapped = degrees % 360f;
        if (wrapped > 180f)
        {
            wrapped -= 360f;
        }
        else if (wrapped <= -180f)
        {
            wrapped += 360f;
        }

        return MathF.Abs(wrapped) < 1e-4f ? 0f : wrapped;
    }

    /// <summary>Parses a comma-separated 3D vector string, returning <paramref name="fallback"/> if incomplete or invalid.</summary>
    public static Vector3 ParseFloat3(string? text, Vector3 fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        string[] parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
        {
            return fallback;
        }

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
            || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
            || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)
            || !float.IsFinite(x)
            || !float.IsFinite(y)
            || !float.IsFinite(z))
        {
            return fallback;
        }

        return new Vector3(x, y, z);
    }

    /// <summary>Formats a float component cleanly using invariant culture and avoiding negative zero.</summary>
    public static string FormatComponent(float value, int decimals = 3)
    {
        if (!float.IsFinite(value))
        {
            return "0";
        }

        double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        if (Math.Abs(rounded) < 1e-6)
        {
            rounded = 0d;
        }

        string format = decimals switch
        {
            <= 1 => "0.#",
            2 => "0.##",
            4 => "0.####",
            _ => "0.###",
        };
        return rounded.ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>Formats a 3D vector as <c>X, Y, Z</c> for studio scalar fields.</summary>
    public static string FormatFloat3(Vector3 value)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{FormatComponent(value.X)}, {FormatComponent(value.Y)}, {FormatComponent(value.Z)}");
    }
}
