using System.Numerics;
using Swordfish.Library.Util;

namespace Swordfish.ECS;

public struct TransformComponent : IDataComponent
{
    public Vector3 Position;
    public Quaternion Orientation;
    public Vector3 Scale = Vector3.One;

    public TransformComponent(Vector3 position)
    {
        Position = position;
    }

    public TransformComponent(Vector3 position, Quaternion orientation)
    {
        Position = position;
        Orientation = orientation;
    }

    public TransformComponent(Vector3 position, Quaternion orientation, Vector3 scale)
    {
        Position = position;
        Orientation = orientation;
        Scale = scale;
    }
    
    public void Rotate(Vector3 rotation, bool local = false)
    {
        var eulerQuaternion = Quaternion.CreateFromYawPitchRoll(
            yaw: rotation.Y * MathS.DEGREES_TO_RADIANS,
            pitch: rotation.X * MathS.DEGREES_TO_RADIANS, rotation.Z * MathS.DEGREES_TO_RADIANS
        );
        
        if (local)
        {
            Orientation = Quaternion.Multiply(Orientation, eulerQuaternion);
        }
        else
        {
            Orientation = Quaternion.Multiply(eulerQuaternion, Orientation);
        }
    }

    public readonly Vector3 GetForward()
    {
        return Vector3.Transform(Vector3.UnitZ, Orientation);
    }

    public readonly Vector3 GetRight()
    {
        return Vector3.Transform(Vector3.UnitX, Orientation);
    }

    public readonly Vector3 GetUp()
    {
        return Vector3.Transform(Vector3.UnitY, Orientation);
    }
    
    public readonly Matrix4x4 ToMatrix4X4()
    {
        Vector3 forward = GetForward();
        Vector3 up = GetUp();
        Vector3 right = GetRight();

        var matrix = new Matrix4x4
        {
            M11 = right.X * Scale.X,
            M12 = right.Y * Scale.X,
            M13 = right.Z * Scale.X,
            M14 = 0,

            M21 = up.X * Scale.Y,
            M22 = up.Y * Scale.Y,
            M23 = up.Z * Scale.Y,
            M24 = 0,

            M31 = forward.X * Scale.Z,
            M32 = forward.Y * Scale.Z,
            M33 = forward.Z * Scale.Z,
            M34 = 0,

            M41 = Position.X,
            M42 = Position.Y,
            M43 = Position.Z,
            M44 = 1,
        };

        return matrix;
    }
}
