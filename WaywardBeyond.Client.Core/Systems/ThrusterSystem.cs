using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class ThrusterSystem : EntitySystem<ThrusterComponent, PhysicsComponent>
{
    protected override void OnTick(float delta, DataStore store, int entity, ref ThrusterComponent thruster, ref PhysicsComponent physics)
    {
        if (thruster.Power <= 0)
        {
            return;
        }

        if (!store.TryGet(entity, out TransformComponent transform))
        {
            return;
        }
        
        physics.Velocity += transform.GetForward() * -(thruster.Power * 10 * delta);
    }
}