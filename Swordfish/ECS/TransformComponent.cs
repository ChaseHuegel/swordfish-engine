using System.Numerics;

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

    public Vector3 GetForward()
    {
        return Vector3.Transform(Vector3.UnitZ, Orientation);
    }

    public Vector3 GetRight()
    {
        return Vector3.Transform(Vector3.UnitX, Orientation);
    }

    public Vector3 GetUp()
    {
        return Vector3.Transform(Vector3.UnitY, Orientation);
    }
}
