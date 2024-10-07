using System.Numerics;

namespace Swordfish.ECS;

[Component]
public class PhysicsComponent
{
    public const int DefaultIndex = 2;

    public Vector3 Velocity;
    public Vector3 Impulse;
    public Quaternion Torque;
    public float Mass = 1f;
    public float Restitution = 0.5f;
    public float Drag = 0.05f;

    public PhysicsComponent() { }

    public PhysicsComponent(Vector3 velocity, Quaternion torque)
    {
        Velocity = velocity;
        Torque = torque;
    }
}
