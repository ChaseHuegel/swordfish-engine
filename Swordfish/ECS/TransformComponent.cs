using System.Numerics;

namespace Swordfish.ECS;

[Component]
public class TransformComponent
{
    public const int DefaultIndex = 1;

    public Vector3 Position;
    public Quaternion Orientation;
    public Vector3 Scale = Vector3.One;

    public TransformComponent() { }

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
}
