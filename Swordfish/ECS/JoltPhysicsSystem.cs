using JoltPhysicsSharp;

namespace Swordfish.ECS;

[ComponentSystem(typeof(PhysicsComponent), typeof(TransformComponent))]
public class JoltPhysicsSystem : ComponentSystem
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
    private float _accumulator = 0f;

    public JoltPhysicsSharp.PhysicsSystem _system;
    public BodyInterface _bodyInterface;

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
            throw new Exception();
        }

        Foundation.SetTraceHandler(Console.WriteLine);

#if DEBUG
        Foundation.SetAssertFailureHandler((inExpression, inMessage, inFile, inLine) =>
        {
            string message = inMessage ?? inExpression;

            string outMessage = $"[JoltPhysics] Assertion failure at {inFile}:{inLine}: {message}";

            Console.WriteLine(outMessage);

            throw new Exception(outMessage);
        });
#endif

        SetupCollisionFiltering();
        _system = new JoltPhysicsSharp.PhysicsSystem(_settings);
        _bodyInterface = _system.BodyInterface;

        _system.OnContactValidate += OnContactValidate;
        _system.OnContactAdded += OnContactAdded;
        _system.OnContactPersisted += OnContactPersisted;
        _system.OnContactRemoved += OnContactRemoved;
        _system.OnBodyActivated += OnBodyActivated;
        _system.OnBodyDeactivated += OnBodyDeactivated;
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
        _accumulator += deltaTime;

        while (_accumulator >= FIXED_TIMESTEP)
        {
            _system.Update(FIXED_TIMESTEP * TIMESCALE, (int)(1 * TIMESCALE));
            _accumulator -= FIXED_TIMESTEP;
        }
    }

    protected override void Update(Entity entity, float deltaTime)
    {
        PhysicsComponent physics = entity.World.Store.GetAt<PhysicsComponent>(entity.Ptr, PhysicsComponent.DefaultIndex);
        TransformComponent transform = entity.World.Store.GetAt<TransformComponent>(entity.Ptr, TransformComponent.DefaultIndex);

        Body body;
        if (physics.Body.HasValue)
        {
            body = physics.Body.Value;
        }
        else
        {
            BoxShape shape = new(transform.Scale / 2);
            using BodyCreationSettings creationSettings = new(shape, transform.Position, transform.Rotation, (MotionType)physics.BodyType, physics.Layer);
            body = _bodyInterface.CreateBody(creationSettings);
            _bodyInterface.AddBody(body.ID, physics.BodyType == Physics.BodyType.Static ? Activation.DontActivate : Activation.Activate);
            physics.Body = body;
        }

        transform.Position = body.CenterOfMassPosition;
        transform.Rotation = body.Rotation;
    }

    protected virtual ValidateResult OnContactValidate(JoltPhysicsSharp.PhysicsSystem system, in Body body1, in Body body2, Double3 baseOffset, nint collisionResult)
    {
        Console.WriteLine("Contact validate callback");

        // Allows you to ignore a contact before it is created (using layers to not make objects collide is cheaper!)
        return ValidateResult.AcceptAllContactsForThisBodyPair;
    }

    protected virtual void OnContactAdded(JoltPhysicsSharp.PhysicsSystem system, in Body body1, in Body body2, in ContactManifold manifold, in ContactSettings settings)
    {
        Console.WriteLine("A contact was added");
    }

    protected virtual void OnContactPersisted(JoltPhysicsSharp.PhysicsSystem system, in Body body1, in Body body2, in ContactManifold manifold, in ContactSettings settings)
    {
        Console.WriteLine("A contact was persisted");
    }

    protected virtual void OnContactRemoved(JoltPhysicsSharp.PhysicsSystem system, ref SubShapeIDPair subShapePair)
    {
        Console.WriteLine("A contact was removed");
    }

    protected virtual void OnBodyActivated(JoltPhysicsSharp.PhysicsSystem system, in BodyID bodyID, ulong bodyUserData)
    {
        Console.WriteLine("A body got activated");
    }

    protected virtual void OnBodyDeactivated(JoltPhysicsSharp.PhysicsSystem system, in BodyID bodyID, ulong bodyUserData)
    {
        Console.WriteLine("A body went to sleep");
    }
}
