using Krill;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;

namespace WaywardBeyond.Server.Systems;

/// <summary>Server-side decorator hook that attaches or cleans up authoritative components on a brick entity.</summary>
public interface IServerBrickEntityDecorator
{
    /// <summary>Returns whether this decorator applies to <paramref name="brick"/>.</summary>
    bool AppliesTo(Brick brick);

    /// <summary>Attaches authoritative components when a brick entity spawns or restores from persistence.</summary>
    void OnSpawn(DataStore store, int entity, Brick brick, in BrickEntityData? persisted);

    /// <summary>Runs optional cleanup before a brick entity is freed.</summary>
    void OnDespawn(DataStore store, int entity, Brick brick)
    {
    }
}
