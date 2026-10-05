using Swordfish.ECS;
using Swordfish.Physics;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// The per-world physics bundle. Registered under this game-owned service identity so the world graph
/// never resolves the engine's concrete <see cref="Swordfish.Physics.Jolt.JoltPhysicsSystem"/> (which
/// the engine also registers as a root singleton): each world creates and owns its own Jolt system
/// through <see cref="Systems.ServerPhysicsSystem"/>, which is both this service and the world's ticked
/// physics slot.
/// </summary>
public interface IServerWorldPhysics : IPhysics
{
    void Tick(float delta, DataStore store);
}