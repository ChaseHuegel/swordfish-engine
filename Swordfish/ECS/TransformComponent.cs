using System.Numerics;

namespace Swordfish.ECS;

[Component]
public class TransformComponent
{
    public const int DefaultIndex = 1;

    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale = Vector3.One;

    public TransformComponent() { }

    public TransformComponent(Vector3 position)
    {
        Position = position;
    }

    public TransformComponent(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
    }

    public TransformComponent(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }
}
