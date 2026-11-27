using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Krill;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Graphics.Particles;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Server.Systems;

namespace WaywardBeyond.Server.Particles;

/// <summary>
/// Batches server-initiated particle bursts per tick into one RPC notification and manages replicated
/// continuous emitter entities for one world.
/// </summary>
public sealed class ServerParticleService(
    ServerPeers peers,
    SessionManager sessions,
    NetworkReplicationSystem replication,
    ILogger<ServerParticleService> logger
) : IServerParticleService
{
    /// <summary>Maximum one-shot bursts queued for broadcast in a single server tick.</summary>
    public const int MAX_BURSTS_PER_TICK = 128;

    private readonly ServerPeers _peers = peers;
    private readonly SessionManager _sessions = sessions;
    private readonly NetworkReplicationSystem _replication = replication;
    private readonly ILogger<ServerParticleService> _logger = logger;

    private readonly Lock _lock = new();
    private List<QueuedBurst> _queuedBursts = [];
    private List<QueuedBurst> _flushBursts = [];
    private readonly List<ParticleBurstEntry> _filteredEntries = [];
    private ParticleBurstEntry[] _sharedBatchSnapshot = [];
    private ParticleBurstEntry[] _filteredBatchSnapshot = [];
    private bool _hasExcludedBurst;
    private bool _capLoggedThisTick;

    /// <inheritdoc/>
    public Result EmitBurst(in ServerParticleBurst burst)
    {
        if (string.IsNullOrWhiteSpace(burst.EffectId))
        {
            return Result.FromFailure("Particle burst EffectId cannot be empty.");
        }

        if (burst.EffectId.Length > ParticleEmitterCodec.MAX_EFFECT_ID_LENGTH)
        {
            return Result.FromFailure($"Particle burst EffectId exceeds {ParticleEmitterCodec.MAX_EFFECT_ID_LENGTH} characters.");
        }

        Vector3 position = burst.Position;
        Vector3 direction = burst.Direction;
        if (!IsFinite(in position) || !IsFinite(in direction))
        {
            return Result.FromFailure("Particle burst position and direction must be finite.");
        }

        if (!float.IsFinite(burst.SpeedScale) || burst.SpeedScale < 0f)
        {
            return Result.FromFailure("Particle burst SpeedScale must be finite and non-negative.");
        }

        if (!float.IsFinite(burst.SizeScale) || burst.SizeScale <= 0f)
        {
            return Result.FromFailure("Particle burst SizeScale must be finite and positive.");
        }

        if (burst.Count.HasValue && burst.Count.Value <= 0)
        {
            return Result.FromFailure("Particle burst Count must be positive.");
        }

        var entry = new ParticleBurstEntry
        {
            EffectId = burst.EffectId,
            PositionX = burst.Position.X,
            PositionY = burst.Position.Y,
            PositionZ = burst.Position.Z,
            DirectionX = burst.Direction.X,
            DirectionY = burst.Direction.Y,
            DirectionZ = burst.Direction.Z,
            Count = burst.Count,
            SpeedScale = burst.SpeedScale,
            SizeScale = burst.SizeScale,
            TintRgba = burst.TintRgba,
        };

        var capped = false;
        var shouldLogCap = false;
        lock (_lock)
        {
            if (_queuedBursts.Count >= MAX_BURSTS_PER_TICK)
            {
                capped = true;
                if (!_capLoggedThisTick)
                {
                    _capLoggedThisTick = true;
                    shouldLogCap = true;
                }
            }
            else
            {
                _queuedBursts.Add(new QueuedBurst(entry, burst.ExcludeClient));
                if (burst.ExcludeClient != Uuid.Null)
                {
                    _hasExcludedBurst = true;
                }
            }
        }

        if (shouldLogCap)
        {
            _logger.LogDebug("Dropping excess particle bursts after reaching tick limit of {limit}.", MAX_BURSTS_PER_TICK);
        }

        return capped
            ? Result.FromFailure($"Particle burst tick limit ({MAX_BURSTS_PER_TICK}) reached.")
            : Result.FromSuccess();
    }

    /// <inheritdoc/>
    public Result EmitBurst(
        string effectId,
        Vector3 position,
        Vector3 direction,
        int? count = null,
        float speedScale = 1f,
        float sizeScale = 1f,
        uint tintRgba = 0u,
        Uuid excludeClient = default
    ) {
        return EmitBurst(new ServerParticleBurst(effectId, position, direction, count, speedScale, sizeScale, tintRgba, excludeClient));
    }

    /// <inheritdoc/>
    public Result<int> SpawnEmitter(
        DataStore store,
        in ParticleEmitterComponent emitter,
        in Vector3 position,
        in Quaternion orientation,
        Uuid parentEntity = default,
        Uuid excludeEntity = default
    ) {
        string? effectId = ParticleEmitterCodec.ResolveEffectId(in emitter);
        if (!ParticleEmitterCodec.IsValid(effectId, emitter.RateScale, in emitter.Offset, in emitter.LocalDirection, out string error))
        {
            return Result<int>.FromFailure(error);
        }

        if (!TransformCodec.IsValid(in position, in orientation, Vector3.One))
        {
            return Result<int>.FromFailure("Emitter transform is non-finite or has a degenerate orientation.");
        }

        int entity = store.Alloc();
        var normalized = new ParticleEmitterComponent(effectId!, emitter.Playing, emitter.RateScale, emitter.TintRgba)
        {
            Offset = emitter.Offset,
            LocalDirection = emitter.LocalDirection,
        };

        NetworkComponent net = NetworkComponent.Create();
        net.ExcludedRecipientEntity = excludeEntity;
        store.AddOrUpdate(entity, net);
        store.AddOrUpdate(entity, new IdentifierComponent(name: null, tag: "game"));
        store.AddOrUpdate(entity, new TransformComponent(position, orientation, Vector3.One));
        store.AddOrUpdate(entity, normalized);

        if (parentEntity != Uuid.Null)
        {
            store.AddOrUpdate(entity, new ParticleEmitterBindingComponent
            {
                ParentEntity = parentEntity.ToValue(),
            });
        }

        return Result<int>.FromSuccess(entity);
    }

    /// <inheritdoc/>
    public Result<int> SpawnEmitter(
        DataStore store,
        in ParticleEmitterComponent emitter,
        in Vector3 position,
        Uuid parentEntity = default,
        Uuid excludeEntity = default
    ) {
        return SpawnEmitter(store, in emitter, in position, Quaternion.Identity, parentEntity, excludeEntity);
    }

    /// <inheritdoc/>
    public void DespawnEmitter(DataStore store, int entity, Uuid expectedUuid)
    {
        if (entity != Entity.Null && expectedUuid != Uuid.Null && store.GetUuid(entity) == expectedUuid)
        {
            Uuid excludedRecipient = store.TryGet(entity, out NetworkComponent net)
                ? net.ExcludedRecipientEntity
                : Uuid.Null;
            _replication.RequestDespawn(expectedUuid.ToValue(), excludedRecipient);
            store.Free(entity);
        }
    }

    /// <summary>Flushes queued bursts once to non-streaming peers in this world.</summary>
    public void Flush()
    {
        bool hasExcludedBurst;
        lock (_lock)
        {
            _capLoggedThisTick = false;
            if (_queuedBursts.Count == 0)
            {
                return;
            }

            (_queuedBursts, _flushBursts) = (_flushBursts, _queuedBursts);
            hasExcludedBurst = _hasExcludedBurst;
            _hasExcludedBurst = false;
        }

        try
        {
            if (_sessions.Count == 0)
            {
                return;
            }

            if (!hasExcludedBurst)
            {
                ParticleBurstEntry[] entries = ToExactArray(_flushBursts, ref _sharedBatchSnapshot);
                var sharedBatch = new ParticleBurstBatchMessage { Bursts = entries };
                foreach (Uuid clientId in _sessions.Clients)
                {
                    if (_replication.IsStreaming(clientId) || !_peers.TryGet(clientId, out ServerPeer peer))
                    {
                        continue;
                    }

                    if (!peer.Client.ParticleBursts(sharedBatch))
                    {
                        peer.CloseOnRejectedSend(_logger);
                    }
                }

                return;
            }

            foreach (Uuid clientId in _sessions.Clients)
            {
                if (_replication.IsStreaming(clientId) || !_peers.TryGet(clientId, out ServerPeer peer))
                {
                    continue;
                }

                _filteredEntries.Clear();
                for (var i = 0; i < _flushBursts.Count; i++)
                {
                    if (_flushBursts[i].ExcludeClient != clientId)
                    {
                        _filteredEntries.Add(_flushBursts[i].Entry);
                    }
                }

                if (_filteredEntries.Count == 0)
                {
                    continue;
                }

                ParticleBurstEntry[] filteredBatch = ToExactArray(_filteredEntries, ref _filteredBatchSnapshot);
                var batch = new ParticleBurstBatchMessage { Bursts = filteredBatch };
                if (!peer.Client.ParticleBursts(batch))
                {
                    peer.CloseOnRejectedSend(_logger);
                }
            }
        }
        finally
        {
            _flushBursts.Clear();
            _filteredEntries.Clear();
        }
    }

    private static ParticleBurstEntry[] ResizeBatchCache(int count, ref ParticleBurstEntry[] cache)
    {
        if (count == 0)
        {
            return cache.Length == 0 ? cache : cache = [];
        }

        if (cache.Length != count)
        {
            cache = new ParticleBurstEntry[count];
        }

        return cache;
    }

    private static ParticleBurstEntry[] ToExactArray(List<QueuedBurst> source, ref ParticleBurstEntry[] cache)
    {
        ParticleBurstEntry[] target = ResizeBatchCache(source.Count, ref cache);
        for (var i = 0; i < source.Count; i++)
        {
            target[i] = source[i].Entry;
        }

        return target;
    }

    private static ParticleBurstEntry[] ToExactArray(List<ParticleBurstEntry> source, ref ParticleBurstEntry[] cache)
    {
        ParticleBurstEntry[] target = ResizeBatchCache(source.Count, ref cache);
        if (source.Count > 0)
        {
            source.CopyTo(target);
        }

        return target;
    }

    private static bool IsFinite(in Vector3 vector)
    {
        return float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);
    }

    private readonly record struct QueuedBurst(ParticleBurstEntry Entry, Uuid ExcludeClient);
}
