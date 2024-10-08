using System.Numerics;
using JoltPhysicsSharp;
using BodyType = Swordfish.Physics.BodyType;

namespace Swordfish.ECS;

[Component]
public class PhysicsComponent
{
    public const int DefaultIndex = 2;

    public BodyType BodyType;
    public byte Layer;

    public Vector3 Velocity;
    public Quaternion Torque;

    internal Body? Body;

    public PhysicsComponent(byte layer, BodyType type)
    {
        Layer = layer;
        BodyType = type;
    }

    public PhysicsComponent(byte layer, BodyType type, Vector3 velocity, Quaternion torque)
    {
        Layer = layer;
        BodyType = type;
        Velocity = velocity;
        Torque = torque;
    }
}
