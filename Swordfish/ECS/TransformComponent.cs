using System.Numerics;

namespace Swordfish.ECS;

[Component]
public class TransformComponent
{
    public const int DefaultIndex = 1;

    public Vector3 Position;
    public Vector3 Rotation;

    public TransformComponent() { }

    public TransformComponent(Vector3 position, Vector3 rotation)
    {
        Position = position;
        Rotation = rotation;
    }
}
