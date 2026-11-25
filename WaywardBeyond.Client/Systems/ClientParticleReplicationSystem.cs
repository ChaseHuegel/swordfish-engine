using System;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Graphics.Particles;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Networking.Codecs;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Drains inbound server particle bursts into <see cref="IParticleService"/> and synchronizes
/// replicated <see cref="ParticleEmitterBindingComponent"/> parent attachment and local owner muting.
/// </summary>
internal sealed class ClientParticleReplicationSystem(
    in ClientInbound inbound,
    in IParticleService particleService,
    in ILogger<ClientParticleReplicationSystem> logger
) : IEntitySystem
{
    private const int MAX_MISSING_EFFECT_LOG_CACHE = 256;
    private const int MAX_INBOUND_BURSTS_PER_BATCH = 256;

    private readonly ClientInbound _inbound = inbound;
    private readonly IParticleService _particleService = particleService;
    private readonly ILogger<ClientParticleReplicationSystem> _logger = logger;

    private readonly HashSet<Uuid> _managedBindings = [];
    private readonly HashSet<Uuid> _attachedChildren = [];
    private readonly HashSet<Uuid> _seenBindings = [];
    private readonly List<Uuid> _staleBindings = [];
    private readonly HashSet<string> _missingBurstEffects = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        if (WaywardBeyond.GameState < GameState.Playing)
        {
            while (_inbound.ParticleBursts.TryDequeue(out _)) { }
            ReleaseManagedBindings(store);
            _seenBindings.Clear();
            _missingBurstEffects.Clear();
            return;
        }

        DrainParticleBursts();
        SyncEmitterBindings(store);
    }

    private void DrainParticleBursts()
    {
        while (_inbound.ParticleBursts.TryDequeue(out ParticleBurstBatchMessage batch))
        {
            ParticleBurstEntry[]? bursts = batch.Bursts;
            if (bursts == null || bursts.Length == 0)
            {
                continue;
            }

            int limit = Math.Min(bursts.Length, MAX_INBOUND_BURSTS_PER_BATCH);
            for (var i = 0; i < limit; i++)
            {
                ApplyBurst(in bursts[i]);
            }
        }
    }

    private void ApplyBurst(in ParticleBurstEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.EffectId) || entry.EffectId.Length > ParticleEmitterCodec.MAX_EFFECT_ID_LENGTH)
        {
            return;
        }

        var position = new Vector3(entry.PositionX, entry.PositionY, entry.PositionZ);
        var direction = new Vector3(entry.DirectionX, entry.DirectionY, entry.DirectionZ);
        if (!ToolAimGeometry.IsFinite(in position) || !ToolAimGeometry.IsFinite(in direction))
        {
            return;
        }

        if (!float.IsFinite(entry.SpeedScale) || entry.SpeedScale < 0f || !float.IsFinite(entry.SizeScale) || entry.SizeScale <= 0f)
        {
            return;
        }

        if (entry.Count.HasValue && entry.Count.Value <= 0)
        {
            return;
        }

        Result emitted = _particleService.EmitBurst(
            entry.EffectId,
            position,
            direction,
            entry.Count,
            entry.SpeedScale,
            entry.SizeScale,
            entry.TintRgba
        );

        if (!emitted.Success
            && _missingBurstEffects.Count < MAX_MISSING_EFFECT_LOG_CACHE
            && _missingBurstEffects.Add(entry.EffectId))
        {
            _logger.LogWarning("Skipping unknown server burst particle effect \"{EffectId}\": {Message}", entry.EffectId, emitted.Message);
        }
    }

    private void SyncEmitterBindings(DataStore store)
    {
        var findLocal = new FindLocalPlayerAction();
        store.Query<PlayerComponent, FindLocalPlayerAction>(0f, ref findLocal);
        Uuid localPlayerUuid = findLocal.PlayerUuid;

        _seenBindings.Clear();
        SyncBindingsAction syncAction = new()
        {
            Owner = this,
            LocalPlayerUuid = localPlayerUuid,
        };
        store.Query<ParticleEmitterComponent, ParticleEmitterBindingComponent, SyncBindingsAction>(0f, ref syncAction);

        if (_managedBindings.Count == _seenBindings.Count)
        {
            return;
        }

        _staleBindings.Clear();
        foreach (Uuid tracked in _managedBindings)
        {
            if (!_seenBindings.Contains(tracked))
            {
                _staleBindings.Add(tracked);
            }
        }

        for (var i = 0; i < _staleBindings.Count; i++)
        {
            Uuid tracked = _staleBindings[i];
            _managedBindings.Remove(tracked);
            ReleaseTrackedEntityComponents(store, tracked);
        }
    }

    private void ReleaseManagedBindings(DataStore store)
    {
        if (_managedBindings.Count == 0)
        {
            _attachedChildren.Clear();
            return;
        }

        foreach (Uuid tracked in _managedBindings)
        {
            ReleaseTrackedEntityComponents(store, tracked);
        }

        _managedBindings.Clear();
        _attachedChildren.Clear();
    }

    private void ReleaseTrackedEntityComponents(DataStore store, Uuid tracked)
    {
        if (store.TryGet(tracked, out int entity))
        {
            if (store.Has(typeof(ParticleEmitterMutedComponent), entity))
            {
                store.Remove(typeof(ParticleEmitterMutedComponent), entity);
            }

            DetachManagedChild(store, entity, tracked);
        }
        else
        {
            _attachedChildren.Remove(tracked);
        }
    }

    private void SyncEntityBinding(
        DataStore store,
        int entity,
        Uuid entityUuid,
        Uuid localPlayerUuid,
        in ParticleEmitterBindingComponent binding
    ) {
        _seenBindings.Add(entityUuid);
        _managedBindings.Add(entityUuid);

        bool excluded = binding.ExcludeEntity != 0UL
            && localPlayerUuid != Uuid.Null
            && binding.ExcludeUuid == localPlayerUuid;

        var parentMissing = false;
        if (binding.ParentEntity != 0UL)
        {
            Uuid parentUuid = binding.ParentUuid;
            if (parentUuid == entityUuid
                || !store.TryGet(parentUuid, out int parentEntity)
                || !store.TryGet(parentEntity, out TransformComponent parentTransform)
                || !TransformCodec.IsValid(in parentTransform.Position, in parentTransform.Orientation, parentTransform.Scale))
            {
                parentMissing = true;
                DetachManagedChild(store, entity, entityUuid);
            }
            else
            {
                if (!store.TryGet(entity, out ChildComponent existingChild) || existingChild.Parent != parentUuid)
                {
                    store.AddOrUpdate(entity, new ChildComponent(parentUuid));
                }

                _attachedChildren.Add(entityUuid);
                if (!store.TryGet(entity, out TransformComponent existingTransform)
                    || existingTransform.Position != parentTransform.Position
                    || existingTransform.Orientation != parentTransform.Orientation
                    || existingTransform.Scale != parentTransform.Scale)
                {
                    store.AddOrUpdate(entity, new TransformComponent(
                        parentTransform.Position,
                        parentTransform.Orientation,
                        parentTransform.Scale
                    ));
                }
            }
        }
        else
        {
            DetachManagedChild(store, entity, entityUuid);
        }

        bool shouldMute = excluded || parentMissing;
        bool isMuted = store.Has(typeof(ParticleEmitterMutedComponent), entity);
        if (shouldMute && !isMuted)
        {
            store.AddOrUpdate(entity, new ParticleEmitterMutedComponent());
        }
        else if (!shouldMute && isMuted)
        {
            store.Remove(typeof(ParticleEmitterMutedComponent), entity);
        }
    }

    private void DetachManagedChild(DataStore store, int entity, Uuid entityUuid)
    {
        if (_attachedChildren.Remove(entityUuid) && store.Has(typeof(ChildComponent), entity))
        {
            store.Remove(typeof(ChildComponent), entity);
        }
    }

    private struct FindLocalPlayerAction : IForEach<PlayerComponent>
    {
        public Uuid PlayerUuid;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player)
        {
            if (PlayerUuid == Uuid.Null)
            {
                PlayerUuid = store.GetUuid(entity);
            }
        }
    }

    private struct SyncBindingsAction : IForEach<ParticleEmitterComponent, ParticleEmitterBindingComponent>
    {
        public ClientParticleReplicationSystem Owner;
        public Uuid LocalPlayerUuid;

        public void Execute(
            float delta,
            DataStore store,
            int entity,
            in ParticleEmitterComponent emitter,
            in ParticleEmitterBindingComponent binding
        ) {
            Owner.SyncEntityBinding(store, entity, store.GetUuid(entity), LocalPlayerUuid, in binding);
        }
    }
}
