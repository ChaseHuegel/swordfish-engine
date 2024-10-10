using JoltPhysicsSharp;
using Swordfish.ECS;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Physics.Jolt;

[ComponentSystem(typeof(PhysicsComponent), typeof(TransformComponent))]
internal class JoltPhysicsSystem : ComponentSystem
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

    public PhysicsSystem _system;
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
        _system = new PhysicsSystem(_settings);
        _bodyInterface = _system.BodyInterface;
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
            _bodyInterface.AddBody(body.ID, physics.BodyType == BodyType.Static ? Activation.DontActivate : Activation.Activate);
            physics.Body = body;
        }

        transform.Position = body.CenterOfMassPosition;
        transform.Rotation = body.Rotation;
    }
}
