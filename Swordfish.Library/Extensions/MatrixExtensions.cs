using System;
using System.Numerics;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Extensions;

public static class MatrixExtensions
{
    public static Vector3 GetRight(this Matrix4x4 matrix)
    {
        return new Vector3(matrix.M11, matrix.M12, matrix.M13);
    }

    public static Vector3 GetUp(this Matrix4x4 matrix)
    {
        return new Vector3(matrix.M21, matrix.M22, matrix.M23);
    }

    public static Vector3 GetForward(this Matrix4x4 matrix)
    {
        return new Vector3(matrix.M31, matrix.M32, matrix.M33);
    }

    public static Vector3 GetPosition(this Matrix4x4 matrix)
    {
        return new Vector3(matrix.M41, matrix.M42, matrix.M43);
    }

    /// <summary>Extracts the camera world position from a view matrix.</summary>
    public static Vector3 GetCameraPosition(this Matrix4x4 viewMatrix)
    {
        if (Matrix4x4.Invert(viewMatrix, out Matrix4x4 invView))
        {
            return invView.Translation;
        }

        return Vector3.Zero;
    }

    /// <summary>Extracts the camera forward direction in world space from a view matrix.</summary>
    public static Vector3 GetCameraForward(this Matrix4x4 viewMatrix)
    {
        var zAxis = new Vector3(viewMatrix.M13, viewMatrix.M23, viewMatrix.M33);
        float lenSq = zAxis.LengthSquared();
        return lenSq > 1e-6f ? -Vector3.Normalize(zAxis) : -Vector3.UnitZ;
    }

    public static float GetProjectionFov(this Matrix4x4 matrix)
    {
        float fovRadians = 2.0f * (float)Math.Atan(1.0f / matrix.M11);
        float fovDegrees = fovRadians * (180.0f / (float)Math.PI);
        return fovDegrees;
    }

    public static float GetProjectionNearPlane(this Matrix4x4 matrix)
    {
        return -matrix.M32 / matrix.M33;
    }

    public static float GetProjectionFarPlane(this Matrix4x4 matrix)
    {
        return (matrix.M32 - 1) / matrix.M33;
    }
}
