using System.Numerics;

namespace Swordfish.ECS;

[Component]
public class TransformComponent
{
    public const int DefaultIndex = 1;

    public Vector3 Position;
    public Vector3 Rotation;
    public Vector3 Scale;

    public TransformComponent() { }

    public TransformComponent(Vector3 position)
    {
        Position = position;
    }

    public TransformComponent(Vector3 position, Vector3 rotation)
    {
        Position = position;
        Rotation = rotation;
    }

    public TransformComponent(Vector3 position, Vector3 rotation, Vector3 scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }
}
