namespace Swordfish.ECS;

/// <summary>Releases Jolt bodies for removed physics components on the physics thread.</summary>
public sealed class PhysicsComponentProcessor : ComponentResourceProcessor<PhysicsComponent>
{
    /// <inheritdoc/>
    protected override void OnRemoved(in PhysicsComponent component, int entity)
    {
        //  The processor runs at the start of the physics tick, on the thread that owns the
        //  physics context. The solver is not using the body, so release it synchronously.
        PhysicsComponent removed = component;
        removed.DisposeBody();
    }
}
