using Swordfish.Library.Extensions;
using JoltPhysicsSharp;
using Swordfish.ECS;
using Swordfish.Library.Threading;
using System.Numerics;
using Microsoft.Extensions.Logging;
using CompoundShape = Swordfish.Library.Types.Shapes.CompoundShape;
using JoltShape = JoltPhysicsSharp.Shape;
using Shape = Swordfish.Library.Types.Shapes.Shape;
using ShapeType = Swordfish.Library.Types.Shapes.ShapeType;

namespace Swordfish.Physics.Jolt;

// ReSharper disable once ClassNeverInstantiated.Global
internal class JoltPhysicsSystem : IEntitySystem, IJoltPhysics, IPhysics
{
    private static class Layers
    {
        public static readonly ObjectLayer NonMoving = Physics.Layers.NON_MOVING;
        public static readonly ObjectLayer Moving = Physics.Layers.MOVING;
    };

    private static class BroadPhaseLayers
    {
        public static readonly BroadPhaseLayer NonMoving = Physics.Layers.NON_MOVING;
        public static readonly BroadPhaseLayer Moving = Physics.Layers.MOVING;
    };

    private const float FIXED_TIMESTEP = 0.016f;
    private const float TIMESCALE = 1f;

    public event EventHandler<EventArgs>? FixedUpdate;

    public PhysicsSystem System { get; }
    
    private readonly bool _accumulateUpdates = false;   //  TODO make this a configurable option
    private readonly BodyInterface _bodyInterface;
    private readonly BroadPhaseLayerFilter _broadPhaseFilter = new SimpleBroadPhaseLayerFilter();
    private readonly ObjectLayerFilter _objectLayerFilter = new SimpleObjectLayerFilter();
    private readonly BodyFilter _bodyFilter = new SimpleBodyFilter();
    private readonly JobSystem _jobSystem;

    private ThreadContext? _context;
    private DataStore? _store;
    private float _accumulator;
    private PhysicsSystemSettings _settings = new()
    {
        MaxBodies = 65536,
        MaxBodyPairs = 65536,
        MaxContactConstraints = 65536,
        NumBodyMutexes = 0,
    };

    public JoltPhysicsSystem(ILogger logger)
    {
        if (!Foundation.Init(doublePrecision: false))
        {
            logger.LogError("[JoltPhysics] Failed to initialize Foundation.");
            throw new Exception("Unable to initialize Jolt Foundation.");
        }

#if DEBUG
        Foundation.SetTraceHandler(message => logger.LogDebug("Jolt debug: {message}", message));

        Foundation.SetAssertFailureHandler((inExpression, inMessage, inFile, inLine) =>
        {
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            string message = inMessage ?? inExpression;
            logger.LogError("[JoltPhysics] Assertion failure at {inFile}:{inLine}: {message}", inFile, inLine, message);
            throw new Exception($"[JoltPhysics] Assertion failure at {inFile}:{inLine}: {message}");    //  TODO is this necessary?
        });
#endif

        SetupCollisionFiltering();
        System = new PhysicsSystem(_settings);
        _jobSystem = new JobSystemThreadPool();
        _bodyInterface = System.BodyInterface;
    }

    public RaycastResult Raycast(in Ray ray)
    {
        if (_context == null || _store == null)
        {
            return default;
        }

        return _context.WaitForResult(JoltRaycastRequest.Invoke, new JoltRaycastRequest(_store, System, ray, _broadPhaseFilter, _objectLayerFilter, _bodyFilter));
    }

    private void SetupCollisionFiltering()
    {
        // We use only 2 layers: one for non-moving objects and one for moving objects
        ObjectLayerPairFilterTable objectLayerPairFilter = new(2);
        objectLayerPairFilter.EnableCollision(Layers.NonMoving, Layers.Moving);
        objectLayerPairFilter.EnableCollision(Layers.Moving, Layers.Moving);

        // We use a 1-to-1 mapping between object layers and broadphase layers
        BroadPhaseLayerInterfaceTable broadPhaseLayerInterface = new(2, 2);
        broadPhaseLayerInterface.MapObjectToBroadPhaseLayer(Layers.NonMoving, BroadPhaseLayers.NonMoving);
        broadPhaseLayerInterface.MapObjectToBroadPhaseLayer(Layers.Moving, BroadPhaseLayers.Moving);

        ObjectVsBroadPhaseLayerFilterTable objectVsBroadPhaseLayerFilter = new(broadPhaseLayerInterface, 2, objectLayerPairFilter, 2);

        _settings.ObjectLayerPairFilter = objectLayerPairFilter;
        _settings.BroadPhaseLayerInterface = broadPhaseLayerInterface;
        _settings.ObjectVsBroadPhaseLayerFilter = objectVsBroadPhaseLayerFilter;
    }

    public void Tick(float delta, DataStore store)
    {
        _store ??= store;
        _context ??= ThreadContext.FromCurrentThread();
        _context.SwitchToCurrentThread();

        _accumulator += delta;

        const float physicsDelta = FIXED_TIMESTEP * TIMESCALE;  //  TODO allow modifying TIMESCALE
        int steps = Math.Max(1, (int)(1f * TIMESCALE));
        if (_accumulateUpdates)
        {
            while (_accumulator >= physicsDelta)
            {
                FixedUpdate?.Invoke(this, EventArgs.Empty);
                _context.ProcessMessageQueue();

                store.Query<PhysicsComponent, TransformComponent>(delta, SyncJoltToEntity);
                System.Update(physicsDelta, steps, _jobSystem);
                store.Query<PhysicsComponent, TransformComponent>(delta, SyncEntityToJolt);

                _accumulator -= physicsDelta;
            }
        }
        else
        {
            if (_accumulator < physicsDelta)
            {
                return;
            }

            FixedUpdate?.Invoke(this, EventArgs.Empty);
            _context.ProcessMessageQueue();

            store.Query<PhysicsComponent, TransformComponent>(delta, SyncJoltToEntity);
            System.Update(physicsDelta, steps, _jobSystem);
            store.Query<PhysicsComponent, TransformComponent>(delta, SyncEntityToJolt);

            _accumulator -= physicsDelta;
        }
    }

    private void SyncEntityToJolt(float delta, DataStore store, int entity, ref PhysicsComponent physics, ref TransformComponent transform)
    {
        if (!store.TryGet(entity, out ColliderComponent collider))
        {
            return;
        }
        
        Body body;
        if (physics.Body != null)
        {
            body = physics.Body;

            transform.Position = body.Position;
            transform.Orientation = body.Rotation;

            physics.Velocity = body.GetLinearVelocity();
            physics.Torque = body.GetAngularVelocity();

            if (collider.SyncedWithPhysics)
            {
                return;
            }

            collider.SyncedWithPhysics = true;
            if (!TryGetJoltShape(collider, transform.Scale, out JoltShape shape))
            {
                return;
            }
            
            _bodyInterface.SetShape(body.ID, shape, true, physics.BodyType == BodyType.Static ? Activation.DontActivate : Activation.Activate);
        }
        else
        {
            collider.SyncedWithPhysics = true;
            if (!TryGetJoltShape(collider, transform.Scale, out JoltShape shape))
            {
                return;
            }

            using BodyCreationSettings creationSettings = new(shape, transform.Position, transform.Orientation, (MotionType)physics.BodyType, physics.Layer);
            body = _bodyInterface.CreateBody(creationSettings);
            _bodyInterface.AddBody(body.ID, physics.BodyType == BodyType.Static ? Activation.DontActivate : Activation.Activate);
            _bodyInterface.SetMotionQuality(body.ID, (MotionQuality)physics.CollisionDetection);
            physics.Body = body;
            physics.BodyID = body.ID;

            SyncJoltToEntity(delta, store, entity, ref physics, ref transform);
        }
    }

    private void SyncJoltToEntity(float delta, DataStore store, int entity, ref PhysicsComponent physics, ref TransformComponent transform)
    {
        if (physics.Body == null)
        {
            return;
        }

        _bodyInterface.SetPositionRotationAndVelocity(physics.Body.ID, transform.Position, transform.Orientation, physics.Velocity, physics.Torque);
    }

    private static bool TryGetJoltShape(ColliderComponent collider, Vector3 scale, out JoltShape joltShape)
    {
        if (collider.CompoundShape.HasValue)
        {
            CompoundShape compoundShape = collider.CompoundShape.Value;
            var mutableCompoundShapeSettings = new MutableCompoundShapeSettings();
            for (var i = 0; i < compoundShape.Shapes.Length; i++)
            {
                Shape childShape = compoundShape.Shapes[i];

                if (!TryGetJoltShape(childShape, scale, out JoltShape childJoltShape))
                {
                    continue;
                }

                mutableCompoundShapeSettings.AddShape(compoundShape.Positions[i] * scale, compoundShape.Orientations[i], childJoltShape);
            }
            joltShape = new MutableCompoundShape(mutableCompoundShapeSettings);
            return true;
        }

        if (collider.Shape.HasValue)
        {
            return TryGetJoltShape(collider.Shape.Value, scale, out joltShape);
        }

        joltShape = default!;
        return false;
    }

    private static bool TryGetJoltShape(Shape shape, Vector3 scale, out JoltShape joltShape)
    {
        switch (shape.Type)
        {
            case ShapeType.Box3:
                joltShape = new BoxShape(shape.Box3.Extents * 0.5f * scale);
                return true;

            case ShapeType.Box2:
                joltShape = new BoxShape(new Vector3(shape.Box2.Extents.X * 0.5f * scale.X, shape.Box2.Extents.Y * 0.5f * scale.Y, 0));
                return true;

            case ShapeType.Sphere:
                joltShape = new SphereShape(shape.Sphere.Radius * (scale.X + scale.Y + scale.Z) / 3);
                return true;

            case ShapeType.Circle:
                joltShape = new SphereShape(shape.Circle.Radius * (scale.X + scale.Y) / 2);
                return true;

            case ShapeType.Capsule:
                joltShape = new CapsuleShape(shape.Capsule.Height * 0.5f * scale.Y, shape.Capsule.Radius * (scale.X + scale.Z) / 2);
                return true;

            case ShapeType.Cylinder:
                joltShape = new CylinderShape(shape.Cylinder.Height * 0.5f * scale.Y, shape.Cylinder.Radius * (scale.X + scale.Z) / 2);
                return true;

            case ShapeType.Plane:
                joltShape = new PlaneShape(new Plane(shape.Plane.Normal, shape.Plane.Distance), null, 5_000);
                return true;
        }

        joltShape = null!;
        return false;
    }
}