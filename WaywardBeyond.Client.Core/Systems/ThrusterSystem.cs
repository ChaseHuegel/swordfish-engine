using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class ThrusterSystem : IEntitySystem
{
    private struct ForEachAction : IForEachRef<ThrusterComponent, PhysicsComponent>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<ThrusterComponent> thruster, ref Ref<PhysicsComponent> physics)
        {
            if (thruster.Read.Power <= 0)
            {
                return;
            }

            if (!store.TryGet(entity, out TransformComponent transform))
            {
                return;
            }

            physics.Write.Velocity += transform.GetForward() * -(thruster.Read.Power * 10 * delta);
        }
    }

    public void Tick(float delta, DataStore store)
    {
        ForEachAction action = default;
        store.QueryRef<ThrusterComponent, PhysicsComponent, ForEachAction>(delta, ref action);
    }
}