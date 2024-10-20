using Swordfish.Library.Extensions;
using JoltPhysicsSharp;
using Swordfish.ECS;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Threading;
using Swordfish.Library.Types.Shapes;
using System.Numerics;
using Plane = Swordfish.Library.Types.Shapes.Plane;
using CompoundShape = Swordfish.Library.Types.Shapes.CompoundShape;

namespace Swordfish.Physics.Jolt;

[ComponentSystem(typeof(PhysicsComponent), typeof(TransformComponent))]
internal partial class JoltPhysicsSystem : ComponentSystem, IJoltPhysics, IPhysics
{
    private static class Layers
    {
        public static readonly ObjectLayer NonMoving = (ObjectLayer)Physics.Layers.NonMoving;
        public static readonly ObjectLayer Moving = (ObjectLayer)Physics.Layers.Moving;
    };

    private static class BroadPhaseLayers
    {
        public static readonly BroadPhaseLayer NonMoving = (BroadPhaseLayer)Physics.Layers.NonMoving;
        public static readonly BroadPhaseLayer Moving = (BroadPhaseLayer)Physics.Layers.Moving;
    };

    private const float FIXED_TIMESTEP = 0.016f;
    private const float TIMESCALE = 1f;

    public event EventHandler<EventArgs>? FixedUpdate;

    public PhysicsSystem System { get; }

    private float _accumulator = 0f;
    private bool _accumulateUpdates = false;
    private BodyInterface _bodyInterface;
    private BroadPhaseLayerFilter _broadPhaseFilter = new SimpleBroadPhaseLayerFilter();
    private ObjectLayerFilter _objectLayerFilter = new SimpleObjectLayerFilter();
    private BodyFilter _bodyFilter = new SimpleBodyFilter();
    private ThreadContext? _context;

    private PhysicsSystemSettings _settings = new()
    {
        MaxBodies = 65536,
        MaxBodyPairs = 65536,
        MaxContactConstraints = 65536,
        NumBodyMutexes = 0
    };

    public JoltPhysicsSystem()
    {
        if (!Foundation.Init(false))
        {
            throw new Exception("Unable to initialize Jolt Foundation.");
        }

#if DEBUG
        Foundation.SetTraceHandler((message) => Debugger.Log(message));

        Foundation.SetAssertFailureHandler((inExpression, inMessage, inFile, inLine) =>
        {
            string message = inMessage ?? inExpression;

            string outMessage = $"[JoltPhysics] Assertion failure at {inFile}:{inLine}: {message}";

            Debugger.LogError(outMessage, null);
            throw new Exception(outMessage);
        });
#endif

        SetupCollisionFiltering();
        System = new PhysicsSystem(_settings);
        _bodyInterface = System.BodyInterface;
    }

    public RaycastResult Raycast(in Ray ray)
    {
        if (_context == null)
        {
            return default;
        }

        return _context.WaitForResult(JoltRaycastRequest.Invoke, new JoltRaycastRequest(Entities, System, ray, _broadPhaseFilter, _objectLayerFilter, _bodyFilter));
    }

    protected virtual void SetupCollisionFiltering()
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

    protected override void Update(float deltaTime)
    {
        _context ??= ThreadContext.PinCurrentThread();

        _accumulator += deltaTime;

        float physicsDelta = FIXED_TIMESTEP * TIMESCALE;
        int steps = Math.Max(1, (int)(1f * TIMESCALE));
        if (_accumulateUpdates)
        {
            while (_accumulator >= physicsDelta)
            {
                FixedUpdate?.Invoke(this, EventArgs.Empty);
                _context.ProcessMessageQueue();

                for (int i = 0; i < Entities.Length; i++)
                {
                    SyncJoltToEntity(Entities[i]);
                }

                System.Update(physicsDelta, steps);

                for (int i = 0; i < Entities.Length; i++)
                {
                    SyncEntityToJolt(Entities[i]);
                }

                _accumulator -= physicsDelta;
            }
        }
        else
        {
            if (_accumulator >= physicsDelta)
            {
                FixedUpdate?.Invoke(this, EventArgs.Empty);
                _context.ProcessMessageQueue();

                for (int i = 0; i < Entities.Length; i++)
                {
                    SyncJoltToEntity(Entities[i]);
                }

                System.Update(physicsDelta, steps);

                for (int i = 0; i < Entities.Length; i++)
                {
                    SyncEntityToJolt(Entities[i]);
                }

                _accumulator -= physicsDelta;
            }
        }
    }

    private void SyncEntityToJolt(Entity entity)
    {
        PhysicsComponent physics = entity.World.Store.GetAt<PhysicsComponent>(entity.Ptr, PhysicsComponent.DefaultIndex);
        TransformComponent transform = entity.World.Store.GetAt<TransformComponent>(entity.Ptr, TransformComponent.DefaultIndex);

        Body body;
        if (physics.Body.HasValue)
        {
            body = physics.Body.Value;

            transform.Position = body.Position;
            transform.Orientation = body.Rotation;

            physics.Velocity = body.GetLinearVelocity();
            physics.Torque = body.GetAngularVelocity();
        }
        else
        {
            ColliderComponent collider = entity.World.Store.GetAt<ColliderComponent>(entity.Ptr, ColliderComponent.DefaultIndex);
            if (collider == null || !TryGetJoltShape(collider.Shape, transform.Scale, out Shape shape))
            {
                return;
            }

            using BodyCreationSettings creationSettings = new(shape, transform.Position, transform.Orientation, (MotionType)physics.BodyType, physics.Layer);
            body = _bodyInterface.CreateBody(creationSettings);
            _bodyInterface.AddBody(body.ID, physics.BodyType == BodyType.Static ? Activation.DontActivate : Activation.Activate);
            _bodyInterface.SetMotionQuality(body.ID, (MotionQuality)physics.CollisionDetection);
            physics.Body = body;
            physics.BodyID = body.ID;

            SyncJoltToEntity(entity, physics, transform);
        }
    }

    private void SyncJoltToEntity(Entity entity, PhysicsComponent? physics = null, TransformComponent? transform = null)
    {
        physics ??= entity.World.Store.GetAt<PhysicsComponent>(entity.Ptr, PhysicsComponent.DefaultIndex);
        if (!physics.Body.HasValue)
        {
            return;
        }

        Body body = physics.Body.Value;
        transform ??= entity.World.Store.GetAt<TransformComponent>(entity.Ptr, TransformComponent.DefaultIndex);

        _bodyInterface.SetPositionRotationAndVelocity(body.ID, transform.Position, transform.Orientation, physics.Velocity, physics.Torque);
    }

    private static bool TryGetJoltShape(IShape shape, Vector3 scale, out Shape joltShape)
    {
        switch (shape)
        {
            case Box3 box3:
                joltShape = new BoxShape(box3.Extents * 0.5f * scale);
                return true;

            case Box2 box2:
                joltShape = new BoxShape(new Vector3(box2.Extents.X * 0.5f * scale.X, box2.Extents.Y * 0.5f * scale.Y, 0));
                return true;

            case Sphere sphere:
                joltShape = new SphereShape(sphere.Radius * (scale.X + scale.Y + scale.Z) / 3);
                return true;

            case Circle circle:
                joltShape = new SphereShape(circle.Radius * (scale.X + scale.Y) / 2);
                return true;

            case Capsule capsule:
                joltShape = new CapsuleShape(capsule.Height * 0.5f * scale.Y, capsule.Radius * (scale.X + scale.Z) / 2);
                return true;

            case Cylinder cylinder:
                joltShape = new CylinderShape(cylinder.Height * 0.5f * scale.Y, cylinder.Radius * (scale.X + scale.Z) / 2);
                return true;

            case Plane plane:
                joltShape = new PlaneShape(new System.Numerics.Plane(plane.Normal, plane.Distance), null, 5_000);
                return true;

            case CompoundShape compoundShape:
                var mutableCompoundShapeSettings = new MutableCompoundShapeSettings();
                for (int i = 0; i < compoundShape.Shapes.Length; i++)
                {
                    IShape childShape = compoundShape.Shapes[i];
                    if (childShape is CompoundShape)
                    {
                        //  Don't allow nesting compound shapes.
                        // TODO raise or log an error.
                        continue;
                    }

                    if (!TryGetJoltShape(childShape, scale, out Shape childJoltShape))
                    {
                        continue;
                    }

                    mutableCompoundShapeSettings.AddShape(compoundShape.Positions[i] * scale, compoundShape.Orientations[i], childJoltShape);
                }
                joltShape = new MutableCompoundShape(mutableCompoundShapeSettings);
                return true;
        }

        joltShape = null!;
        return false;
    }
}