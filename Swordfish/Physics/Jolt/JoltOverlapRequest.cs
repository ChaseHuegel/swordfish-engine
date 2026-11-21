using System.Numerics;
using JoltPhysicsSharp;
using Krill;
using Swordfish.ECS;
using JoltShape = JoltPhysicsSharp.Shape;

namespace Swordfish.Physics.Jolt;

/// <summary>Executes a <see cref="ShapeOverlapQuery"/> on the Jolt narrow-phase query interface.</summary>
internal readonly struct JoltOverlapRequest(
    in DataStore store,
    in PhysicsSystem system,
    in ShapeOverlapQuery query,
    in BroadPhaseLayerFilter broadPhaseLayerFilter,
    in ObjectLayerFilter objectLayerFilter,
    in BodyFilter bodyFilter,
    in IgnoreBodyFilter ignoreBodyFilter,
    in List<CollideShapeResult> results
) {
    private readonly DataStore _store = store;
    private readonly PhysicsSystem _system = system;
    private readonly ShapeOverlapQuery _query = query;
    private readonly BroadPhaseLayerFilter _broadPhaseLayerFilter = broadPhaseLayerFilter;
    private readonly ObjectLayerFilter _objectLayerFilter = objectLayerFilter;
    private readonly BodyFilter _bodyFilter = bodyFilter;
    private readonly IgnoreBodyFilter _ignoreBodyFilter = ignoreBodyFilter;
    private readonly List<CollideShapeResult> _results = results;

    public static bool Invoke(JoltOverlapRequest args)
    {
        if (!JoltPhysicsSystem.TryGetJoltShape(args._query.Shape, args._query.Scale, out JoltShape joltShape))
        {
            return false;
        }

        using (joltShape)
        {
            BodyFilter bodyFilter = args._bodyFilter;
            if (args._query.IgnoredEntity >= 0
                && args._store.TryGet(args._query.IgnoredEntity, out PhysicsComponent ignoredPhysics)
                && ignoredPhysics.BodyID.HasValue)
            {
                args._ignoreBodyFilter.IgnoredBodyID = ignoredPhysics.BodyID.Value;
                bodyFilter = args._ignoreBodyFilter;
            }

            Vector3 centerOfMassWorld = args._query.Position
                + Vector3.Transform(joltShape.CenterOfMass, args._query.Orientation);
            Matrix4x4 centerOfMassTransform = Matrix4x4.CreateFromQuaternion(args._query.Orientation)
                * Matrix4x4.CreateTranslation(centerOfMassWorld);
            Vector3 unitScale = Vector3.One;
            Vector3 baseOffset = Vector3.Zero;
            var settings = new CollideShapeSettings
            {
                MaxSeparationDistance = 0f,
            };

            args._results.Clear();
            try
            {
                CollisionCollectorType collectorType = args._query.MinPenetration <= 0f
                    ? CollisionCollectorType.AnyHit
                    : CollisionCollectorType.AllHit;

                bool hit = args._system.NarrowPhaseQueryNoLock.CollideShape(
                    joltShape,
                    in unitScale,
                    in centerOfMassTransform,
                    in settings,
                    in baseOffset,
                    collectorType,
                    args._results,
                    args._broadPhaseLayerFilter,
                    args._objectLayerFilter,
                    bodyFilter
                );

                if (!hit)
                {
                    return false;
                }

                if (args._query.MinPenetration <= 0f)
                {
                    return true;
                }

                for (var i = 0; i < args._results.Count; i++)
                {
                    if (args._results[i].PenetrationDepth > args._query.MinPenetration)
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                args._results.Clear();
                args._ignoreBodyFilter.IgnoredBodyID = default;
            }
        }
    }
}
