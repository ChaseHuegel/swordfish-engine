using System.Numerics;
using JoltPhysicsSharp;
using Swordfish.Library.Threading;
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

    internal ThreadContext? ThreadContext;
    internal BodyInterface? BodyInterface;
    internal Body? Body;
    internal BodyID? BodyID;
    
    private bool _disposed;

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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ThreadContext?.Post(FinalizeDispose, null);
    }

    //  TODO really need a better way to handle cleaning up native resources tied to components
    private void FinalizeDispose(object? state)
    {
        if (BodyID != null && BodyInterface != null)
        {
            BodyInterface.Value.RemoveAndDestroyBody(BodyID.Value);
            BodyID = null;
            BodyInterface = null;
        }

        if (Body != null)
        {
            Body.Dispose();
            Body = null;
        }
    }
}
