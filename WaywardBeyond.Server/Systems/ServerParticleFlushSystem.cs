using Krill;
using WaywardBeyond.Server.Particles;

namespace WaywardBeyond.Server.Systems;

/// <summary>Flushes queued server-initiated particle bursts once per tick before snapshot publishing.</summary>
public sealed class ServerParticleFlushSystem(ServerParticleService particleService) : IServerWorldSystem
{
    private readonly ServerParticleService _particleService = particleService;

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        _particleService.Flush();
    }
}
