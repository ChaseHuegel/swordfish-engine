using System.Numerics;

namespace Swordfish.ECS;

[ComponentSystem(typeof(TransformComponent), typeof(CollisionComponent))]
public class CollisionSystem : ComponentSystem
{
    private const float FIXED_TIMESTEP = 0.016f;
    private const float TIMESCALE = 1f;
    private float _accumulator = 0f;

    protected override void Update(float deltaTime)
    {
        _accumulator += deltaTime;

        while (_accumulator >= FIXED_TIMESTEP)
        {
            Simulate(FIXED_TIMESTEP * TIMESCALE);
            _accumulator -= FIXED_TIMESTEP;
        }
    }

    protected override void Update(Entity entity, float deltaTime) { }

    private void Simulate(float delta)
    {
        for (int i = 0; i < Entities.Length; i++)
        {
            Entity entity = Entities[i];

            TransformComponent transform1 = entity.World.Store.GetAt<TransformComponent>(entity.Ptr, TransformComponent.DefaultIndex);
            transform1.PositionOld = transform1.Position;

            bool physics1Enabled = entity.World.Store.HasAt(entity.Ptr, PhysicsComponent.DefaultIndex);
            if (!physics1Enabled)
                continue;

            PhysicsComponent physics1 = entity.World.Store.GetAt<PhysicsComponent>(entity.Ptr, PhysicsComponent.DefaultIndex);

            physics1.Velocity *= 1f - (physics1.Drag * delta);

            physics1.Impulse *= 1f - delta;

            if (physics1.Velocity.Length() <= delta)
                physics1.Velocity = Vector3.Zero;

            transform1.Position += physics1.Velocity * delta;
            transform1.Position.Y -= 9.8f * delta;
        }

        for (int step = 0; step < 1; step++)
        {
            for (int i = 0; i < Entities.Length; i++)
            {
                Entity entity = Entities[i];
                bool physics1Enabled = entity.World.Store.HasAt(entity.Ptr, PhysicsComponent.DefaultIndex);
                PhysicsComponent? physics1 = physics1Enabled ? entity.World.Store.GetAt<PhysicsComponent>(entity.Ptr, PhysicsComponent.DefaultIndex) : null;
                TransformComponent transform1 = entity.World.Store.GetAt<TransformComponent>(entity.Ptr, TransformComponent.DefaultIndex);
                CollisionComponent collision1 = entity.World.Store.GetAt<CollisionComponent>(entity.Ptr, CollisionComponent.DefaultIndex);

                var startPosition = transform1.PositionOld;
                var endPosition = transform1.Position;
                var curPosition = transform1.PositionOld;
                var posChange = endPosition - startPosition;
                var substepCount = Math.Max(8, (int)posChange.Length());
                var posStep = posChange / substepCount;

                for (int substep = 0; substep < substepCount; substep++)
                {
                    curPosition += posStep;

                    for (int n = 0; n < Entities.Length; n++)
                    {
                        Entity other = Entities[n];
                        if (entity.Ptr == other.Ptr)
                            continue;

                        bool physics2Enabled = entity.World.Store.HasAt(other.Ptr, PhysicsComponent.DefaultIndex);
                        PhysicsComponent? physics2 = physics2Enabled ? entity.World.Store.GetAt<PhysicsComponent>(other.Ptr, PhysicsComponent.DefaultIndex) : null;
                        TransformComponent transform2 = entity.World.Store.GetAt<TransformComponent>(other.Ptr, TransformComponent.DefaultIndex);
                        CollisionComponent collision2 = entity.World.Store.GetAt<CollisionComponent>(other.Ptr, CollisionComponent.DefaultIndex);

                        Vector3 center1 = curPosition;
                        Vector3 center2 = transform2.Position;
                        float radius1 = 0.5f * transform1.Scale.Y, radius2 = 0.5f * transform2.Scale.Y;

                        Vector3 vector = center1 - center2;
                        float dot = Vector3.Dot(vector, vector);
                        float range = radius1 + radius2;
                        float distance = dot - range * range;

                        if (distance >= 0)
                            continue;

                        float depth = Math.Abs(distance);
                        Vector3 normal = Vector3.Normalize(vector);
                        float normalLength = normal.Length();
                        float skin = collision1.Skin + collision2.Skin;

                        float energy = (physics1?.Velocity.Length() ?? 0 - physics2?.Velocity.Length() ?? 0) * normalLength / (normalLength * normalLength * (1f / physics1?.Mass ?? 1f + 1f / physics2?.Mass ?? 1f));

                        //  Physics solving
                        if (physics1 != null)
                        {
                            float response1 = physics1.Restitution * energy;
                            physics1.Velocity += physics1.Velocity - response1 * normal / (physics2?.Mass ?? 1f);
                        }

                        if (physics2 != null)
                        {
                            float response2 = physics2.Restitution * energy;
                            physics2.Velocity += physics1?.Velocity ?? Vector3.Zero + response2 * normal / (physics1?.Mass ?? 1f);
                        }

                        //  Collision solving
                        float solverMod1 = physics1 != null ? 0.5f : 0f;
                        float solverMod2 = physics2 != null ? 0.5f : 0f;

                        if (solverMod1 == 0f && solverMod2 != 0f)
                        {
                            solverMod2 = 1f;
                        }

                        if (solverMod2 == 0f && solverMod1 != 0f)
                        {
                            solverMod1 = 1f;
                        }

                        curPosition += normal * (depth + skin) * solverMod1 * (2f * physics1?.Restitution ?? 0f);
                        transform2.Position += -normal * (depth + skin) * solverMod2 * (2f * physics2?.Restitution ?? 0f);
                    }
                }

                transform1.Position = curPosition;
            }
        }
    }

    internal struct Collision
    {
        public int A, B;
        public Vector3 Contact;
        public Vector3 Normal;
        public float Penetration;
    }

    private static float SphereToSphere(Vector3 center1, float radius1, Vector3 center2, float radius2)
    {
        Vector3 relativeVector = center1 - center2;
        float range = radius1 + radius2;
        return Vector3.Dot(relativeVector, relativeVector) - range * range;
    }
}