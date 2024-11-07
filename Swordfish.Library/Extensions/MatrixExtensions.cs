using System;
using System.Numerics;

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