using System.Numerics;
using JoltPhysicsSharp;
using Swordfish.Physics;
using BodyType = Swordfish.Physics.BodyType;

namespace Swordfish.ECS;

public struct PhysicsComponent : IDataComponent, IDisposable
{
    public readonly byte Layer;
    public readonly BodyType BodyType;
    public readonly CollisionDetection CollisionDetection;

    public Vector3 Velocity;
    public Vector3 Torque;

    internal BodyInterface? BodyInterface;
    internal Body? Body;
    internal BodyID? BodyID;
    internal bool Disposing;
    
    internal bool Disposed => Disposing && Body == null;

    public PhysicsComponent(byte layer, BodyType type, CollisionDetection collisionDetection)
    {
        Layer = layer;
        BodyType = type;
        CollisionDetection = collisionDetection;
    }

    // ReSharper disable once UnusedMember.Global
    public PhysicsComponent(byte layer, BodyType type, CollisionDetection collisionDetection, Vector3 velocity, Vector3 torque)
    {
        Layer = layer;
        BodyType = type;
        CollisionDetection = collisionDetection;
        Velocity = velocity;
        Torque = torque;
    }

    public void Dispose()
    {
        Disposing = true;
    }

    //  TODO really need a better way to handle cleaning up native resources tied to components
    internal void FinalizeDispose()
    {
        if (Disposed)
        {
            return;
        }
        
        if (BodyID != null && BodyInterface != null)
        {
            BodyID = null;
            BodyInterface = null;
            BodyInterface?.RemoveAndDestroyBody(BodyID.Value);
        }

        if (Body != null)
        {
            Body.Dispose();
            Body = null;
        }
    }
}
