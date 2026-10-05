using Swordfish.ECS;
using Swordfish.Physics.Jolt;
using WaywardBeyond.Server.Core;

namespace WaywardBeyond.Server.Core.Systems;

/// <summary>
/// Positions the world's physics system in the world's tick order. The instance is the world-scoped
/// <see cref="JoltPhysicsSystem"/>, resolved by concrete type: the engine's own registration is a root
/// singleton that the client's runtime uses, and resolution through a world scope returns the scoped
/// per-world instance.
/// </summary>
public sealed class ServerPhysicsSystem : IServerWorldSystem
{
    private readonly JoltPhysicsSystem _physics;

    public ServerPhysicsSystem(in JoltPhysicsSystem physics)
    {
        _physics = physics;
    }

    public void Tick(float delta, DataStore store)
    {
        _physics.Tick(delta, store);
    }
}