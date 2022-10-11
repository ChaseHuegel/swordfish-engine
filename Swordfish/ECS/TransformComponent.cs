using System.Numerics;

namespace Swordfish.ECS;

[Component]
public class TransformComponent
{
    public const int DefaultIndex = 1;

    public Vector3 Position;
    public Quaternion Rotation;

    public TransformComponent() { }

    public TransformComponent(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
    }
}
