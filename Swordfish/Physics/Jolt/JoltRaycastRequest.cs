using System.Numerics;
using JoltPhysicsSharp;
using Swordfish.ECS;

namespace Swordfish.Physics.Jolt;

internal readonly struct JoltRaycastRequest(in DataStore store, in PhysicsSystem system, in Ray ray, in BroadPhaseLayerFilter broadPhaseLayerFilter, in ObjectLayerFilter objectLayerFilter, in BodyFilter bodyFilter)
{
    private readonly DataStore _store = store;
    private readonly PhysicsSystem _system = system;
    private readonly Ray _ray = ray;
    private readonly BroadPhaseLayerFilter _broadPhaseLayerFilter = broadPhaseLayerFilter;
    private readonly ObjectLayerFilter _objectLayerFilter = objectLayerFilter;
    private readonly BodyFilter _bodyFilter = bodyFilter;

    public static RaycastResult Invoke(JoltRaycastRequest args)
    {
        var ray = new JoltPhysicsSharp.Ray(args._ray.Origin, args._ray.Vector);
        
        bool rayHit = args._system.NarrowPhaseQueryNoLock.CastRay(ray, out RayCastResult result, args._broadPhaseLayerFilter, args._objectLayerFilter, args._bodyFilter);
        
        Vector3 hitPoint = rayHit ? args._ray.Origin + args._ray.Vector * result.Fraction : args._ray.Origin + args._ray.Vector;

        if (!rayHit)
        {
            return new RaycastResult(false, new Entity(), hitPoint, default);
        }

        if (!args._store.Find<PhysicsComponent>(physicsComponent => result.BodyID.Equals(physicsComponent.BodyID), out int entity))
        {
            return new RaycastResult(false, default, hitPoint, default);
        }

        args._system.BodyLockInterface.LockRead(result.BodyID, out BodyLockRead bodyLock);
        Body? body = bodyLock.Body;
        if (body == null)
        {
            args._system.BodyLockInterface.UnlockRead(in bodyLock);
            return new RaycastResult(false, default, hitPoint, default);
        }

        Vector3 normal = body.GetWorldSpaceSurfaceNormal(result.subShapeID2, hitPoint);
        args._system.BodyLockInterface.UnlockRead(in bodyLock);

        return new RaycastResult(true, new Entity(entity, args._store), hitPoint, normal);
    }
}
