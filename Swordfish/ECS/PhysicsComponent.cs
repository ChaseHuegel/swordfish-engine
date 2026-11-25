using System.Numerics;
using JoltPhysicsSharp;
using Krill;
using Swordfish.Library.Threading;
using Swordfish.Physics;
using BodyType = Swordfish.Physics.BodyType;

namespace Swordfish.ECS;

public struct PhysicsComponent : IDataComponent
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

    /// <summary>Releases the Jolt body, marshalling to the physics thread when a context is bound.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (ThreadContext != null)
        {
            ThreadContext.Post(Release, null);
        }
        else
        {
            Release(null);
        }
    }

    /// <summary>Releases the body on the calling thread. The caller must own the physics context.</summary>
    internal void DisposeBody()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Release(null);
    }

    private void Release(object? state)
    {
        if (BodyID != null && BodyInterface != null)
        {
            //  A second release can see the same body if a caller disposed a component copy
            //  before the removal was captured. Jolt reports whether the body is still added.
            if (BodyInterface.Value.IsAdded(BodyID.Value))
            {
                BodyInterface.Value.RemoveAndDestroyBody(BodyID.Value);
            }

            BodyID = null;
            BodyInterface = null;
        }

        if (Body != null)
        {
            if (!Body.IsDisposed)
            {
                Body.Dispose();
            }

            Body = null;
        }
    }
}
