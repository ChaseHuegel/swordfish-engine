using System.Numerics;
using JoltPhysicsSharp;
using Swordfish.ECS;
using Swordfish.Library.Collections;

namespace Swordfish.Physics.Jolt;

internal readonly struct JoltRaycastRequest(Entity[] entities, PhysicsSystem system, Ray ray, BroadPhaseLayerFilter broadPhaseLayerFilter, ObjectLayerFilter objectLayerFilter, BodyFilter bodyFilter)
{
    private readonly Entity[] entities = entities;
    private readonly PhysicsSystem system = system;
    private readonly Ray ray = ray;
    private readonly BroadPhaseLayerFilter broadPhaseLayerFilter = broadPhaseLayerFilter;
    private readonly ObjectLayerFilter objectLayerFilter = objectLayerFilter;
    private readonly BodyFilter bodyFilter = bodyFilter;

    public static RaycastResult Invoke(JoltRaycastRequest args)
    {
        bool rayHit = args.system.NarrowPhaseQuery.CastRay(args.ray.Origin, args.ray.Vector, out RayCastResult result, args.broadPhaseLayerFilter, args.objectLayerFilter, args.bodyFilter);
        Vector3 hitPoint = rayHit ? args.ray.Origin + args.ray.Vector * result.Fraction : args.ray.Origin + args.ray.Vector;

        if (!rayHit)
        {
            return new RaycastResult(false, new Entity(), hitPoint);
        }

        ECSContext world = args.entities[0].World;
        DataPtr<PhysicsComponent> match = world.Store
            .EnumerateEachOf<PhysicsComponent>(PhysicsComponent.DefaultIndex)
            .FirstOrDefault(match => result.BodyID.Equals(match.Data.BodyID));

        if (match.Ptr == Entity.Null)
        {
            return new RaycastResult(false, new Entity(), hitPoint);
        }

        var entity = new Entity(match.Ptr, world);
        return new RaycastResult(true, entity, hitPoint);
    }
}
