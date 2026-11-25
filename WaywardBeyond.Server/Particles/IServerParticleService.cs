using System.Numerics;
using Krill;
using Swordfish.Graphics.Particles;
using Swordfish.Library.Util;

namespace WaywardBeyond.Server.Particles;

/// <summary>Server-side per-world service for queuing replicated particle bursts and managing replicated emitters.</summary>
public interface IServerParticleService
{
    /// <summary>Queues a one-shot particle burst for broadcast at the end of the current world tick.</summary>
    Result EmitBurst(in ServerParticleBurst burst);

    /// <summary>Queues a one-shot particle burst for broadcast at the end of the current world tick.</summary>
    Result EmitBurst(
        string effectId,
        Vector3 position,
        Vector3 direction,
        int? count = null,
        float speedScale = 1f,
        float sizeScale = 1f,
        uint tintRgba = 0u,
        Uuid excludeClient = default
    );

    /// <summary>Spawns a replicated continuous particle emitter entity in <paramref name="store"/>.</summary>
    Result<int> SpawnEmitter(
        DataStore store,
        in ParticleEmitterComponent emitter,
        in Vector3 position,
        in Quaternion orientation,
        Uuid parentEntity = default,
        Uuid excludeEntity = default
    );

    /// <summary>Spawns an identity-oriented replicated continuous particle emitter entity in <paramref name="store"/>.</summary>
    Result<int> SpawnEmitter(
        DataStore store,
        in ParticleEmitterComponent emitter,
        in Vector3 position,
        Uuid parentEntity = default,
        Uuid excludeEntity = default
    );

    /// <summary>Schedules despawn replication and frees an emitter entity if <paramref name="expectedUuid"/> still owns it.</summary>
    void DespawnEmitter(DataStore store, int entity, Uuid expectedUuid);
}
