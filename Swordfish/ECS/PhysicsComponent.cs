using System.Numerics;

namespace Swordfish.ECS;

[Component]
public class PhysicsComponent
{
    public const int DefaultIndex = 2;

    public Vector3 Velocity;
    public Quaternion Torque;

    public PhysicsComponent() { }

    public PhysicsComponent(Vector3 velocity, Quaternion torque)
    {
        Velocity = velocity;
        Torque = torque;
    }
}
