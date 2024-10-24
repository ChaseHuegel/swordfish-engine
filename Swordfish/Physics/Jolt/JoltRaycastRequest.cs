using System.Numerics;
using JoltPhysicsSharp;
using Swordfish.ECS;

namespace Swordfish.Physics.Jolt;

internal readonly struct JoltRaycastRequest(in DataStore store, in PhysicsSystem system, in Ray ray, in BroadPhaseLayerFilter broadPhaseLayerFilter, in ObjectLayerFilter objectLayerFilter, in BodyFilter bodyFilter)
{
    private readonly DataStore store = store;
    private readonly PhysicsSystem system = system;
    private readonly Ray ray = ray;
    private readonly BroadPhaseLayerFilter broadPhaseLayerFilter = broadPhaseLayerFilter;
    private readonly ObjectLayerFilter objectLayerFilter = objectLayerFilter;
    private readonly BodyFilter bodyFilter = bodyFilter;

    public static RaycastResult Invoke(JoltRaycastRequest args)
    {
        bool rayHit = args.system.NarrowPhaseQueryNoLock.CastRay(args.ray.Origin, args.ray.Vector, out RayCastResult result, args.broadPhaseLayerFilter, args.objectLayerFilter, args.bodyFilter);
        Vector3 hitPoint = rayHit ? args.ray.Origin + args.ray.Vector * result.Fraction : args.ray.Origin + args.ray.Vector;

        if (!rayHit)
        {
            return new RaycastResult(false, new Entity(), hitPoint);
        }

        if (args.store.Find<PhysicsComponent>(physicsComponent => result.BodyID.Equals(physicsComponent.BodyID), out int entity))
        {
            return new RaycastResult(true, new Entity(entity, args.store), hitPoint);
        }

        return new RaycastResult(false, default, hitPoint);
    }
}
