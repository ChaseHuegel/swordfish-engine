using System.Numerics;
using JoltPhysicsSharp;
using Swordfish.Physics;
using BodyType = Swordfish.Physics.BodyType;

namespace Swordfish.ECS;

[Component]
public class PhysicsComponent
{
    public const int DefaultIndex = 2;

    public readonly byte Layer;
    public readonly BodyType BodyType;
    public readonly CollisionDetection CollisionDetection;

    public Vector3 Velocity;
    public Vector3 Torque;

    internal Body? Body;
    internal BodyID? BodyID;

    public PhysicsComponent(byte layer, BodyType type, CollisionDetection collisionDetection)
    {
        Layer = layer;
        BodyType = type;
        CollisionDetection = collisionDetection;
    }

    public PhysicsComponent(byte layer, BodyType type, CollisionDetection collisionDetection, Vector3 velocity, Vector3 torque)
    {
        Layer = layer;
        BodyType = type;
        CollisionDetection = collisionDetection;
        Velocity = velocity;
        Torque = torque;
    }
}
