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
        bool rayHit = args._system.NarrowPhaseQueryNoLock.CastRay(args._ray.Origin, args._ray.Vector, out RayCastResult result, args._broadPhaseLayerFilter, args._objectLayerFilter, args._bodyFilter);
        Vector3 hitPoint = rayHit ? args._ray.Origin + args._ray.Vector * result.Fraction : args._ray.Origin + args._ray.Vector;

        if (!rayHit)
        {
            return new RaycastResult(false, new Entity(), hitPoint);
        }

        if (args._store.Find<PhysicsComponent>(physicsComponent => result.BodyID.Equals(physicsComponent.BodyID), out int entity))
        {
            return new RaycastResult(true, new Entity(entity, args._store), hitPoint);
        }

        return new RaycastResult(false, default, hitPoint);
    }
}
