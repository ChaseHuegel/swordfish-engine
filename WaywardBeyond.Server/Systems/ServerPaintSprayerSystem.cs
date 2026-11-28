using System;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Graphics.Particles;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Items;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Particles;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Maintains replicated nozzle and surface-impact <see cref="ParticleEmitterComponent"/> entities
/// through <see cref="IServerParticleService"/> while players spray with a paint sprayer equipped.
/// </summary>
public sealed class ServerPaintSprayerSystem(
    IServerWorldPhysics physics,
    IItemDatabase itemDatabase,
    SimulationStep simulationStep,
    IServerParticleService particles,
    IPaintDatabase? paintDatabase = null
) : IServerWorldSystem
{
    private const float TARGET_DELTA_SQ_THRESHOLD = 0.0004f;
    private const float NORMAL_DELTA_SQ_THRESHOLD = 0.001f;
    private const string STREAM_EFFECT_ID = "paint_sprayer_stream";
    private const string IMPACT_EFFECT_ID = "paint_sprayer_impact";
    private const string SOLVENT_STREAM_EFFECT_ID = "paint_sprayer_solvent_stream";
    private const string SOLVENT_IMPACT_EFFECT_ID = "paint_sprayer_solvent_impact";

    private readonly IServerWorldPhysics _physics = physics;
    private readonly IItemDatabase _itemDatabase = itemDatabase;
    private readonly SimulationStep _simulationStep = simulationStep;
    private readonly IServerParticleService _particles = particles;
    private readonly IPaintDatabase? _paintDatabase = paintDatabase;

    private readonly Dictionary<Uuid, SprayerEmitterPair> _sprayers = [];
    private readonly HashSet<Uuid> _seenPlayers = [];
    private readonly List<Uuid> _stalePlayers = [];

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        Tick(store, _simulationStep.CurrentSimTick);
    }

    /// <summary>Evaluates active spray emitters for every player mirror against <paramref name="simTick"/>.</summary>
    public void Tick(DataStore store, uint simTick)
    {
        _seenPlayers.Clear();

        UpdateAction action = new()
        {
            Owner = this,
            SimTick = simTick,
        };
        store.Query<NetworkComponent, OwnedCharacterComponent, TransformComponent, InputComponent, UpdateAction>(0f, ref action);

        ReleaseStaleSprayers(store);
    }

    private void UpdatePlayerSprayer(
        DataStore store,
        int entity,
        uint simTick,
        in NetworkComponent net,
        in TransformComponent transform,
        in InputComponent latestInput
    ) {
        if (store.TryGet(entity, out StatisticsComponent stats) && stats.IsDead)
        {
            return;
        }

        InputComponent input = ResolveInput(in net, in latestInput, simTick, _simulationStep.CommandMaxAgeTicks);
        bool firing = input.PrimaryHeld || input.SecondaryHeld;
        if (!firing
            || !TransformCodec.IsValid(in transform.Position, in transform.Orientation, transform.Scale)
            || !IsHoldingSprayerTool(store, entity))
        {
            return;
        }

        bool solvent = !input.PrimaryHeld;
        string streamEffectId;
        string impactEffectId;
        uint tintRgba;
        if (solvent)
        {
            streamEffectId = SOLVENT_STREAM_EFFECT_ID;
            impactEffectId = SOLVENT_IMPACT_EFFECT_ID;
            tintRgba = 0u;
        }
        else
        {
            streamEffectId = STREAM_EFFECT_ID;
            impactEffectId = IMPACT_EFFECT_ID;
            tintRgba = ResolveColorTint(input.PaintId);
        }

        bool hit = AimRay.TryTrace(
            _physics,
            entity,
            in transform,
            CharacterDefaults.DEFAULT_REACH,
            out Vector3 target,
            out Vector3 normal,
            out _
        );

        if (!ToolAimGeometry.IsFinite(in target) || !ToolAimGeometry.IsFinite(in normal))
        {
            return;
        }

        Vector3 remoteOrigin = ToolAimGeometry.ComputeRemoteOrigin(in transform);
        Vector3 startWorld = ToolAimGeometry.ComputeSafeStart(in transform, remoteOrigin, target);
        Vector3 sprayDir = ToolAimGeometry.ComputeAimDirection(in transform, startWorld, target);
        if (!ToolAimGeometry.IsFinite(in startWorld) || !ToolAimGeometry.IsFinite(in sprayDir))
        {
            return;
        }

        Quaternion normalizedRot = Quaternion.Normalize(transform.Orientation);
        Quaternion invRot = Quaternion.Conjugate(normalizedRot);
        Vector3 localOffset = Vector3.Transform(startWorld - transform.Position, invRot);
        Vector3 rawLocalDir = Vector3.Transform(sprayDir, invRot);
        float localDirLenSq = rawLocalDir.LengthSquared();
        Vector3 localDir = float.IsFinite(localDirLenSq) && localDirLenSq > 1e-6f
            ? rawLocalDir / MathF.Sqrt(localDirLenSq)
            : -Vector3.UnitZ;

        Vector3 safeNormal = normal.LengthSquared() > 1e-6f ? Vector3.Normalize(normal) : Vector3.UnitY;
        Vector3 impactPos = target + safeNormal * ToolAimGeometry.IMPACT_EMITTER_OFFSET;
        if (!ToolAimGeometry.IsFinite(in localOffset) || !ToolAimGeometry.IsFinite(in impactPos))
        {
            return;
        }

        var nozzleEmitter = new ParticleEmitterComponent(streamEffectId, playing: true, tintRgba: tintRgba)
        {
            Offset = localOffset,
            LocalDirection = localDir,
        };
        var impactEmitter = new ParticleEmitterComponent(impactEffectId, playing: hit, tintRgba: tintRgba)
        {
            LocalDirection = safeNormal,
        };

        Uuid playerUuid = store.GetUuid(entity);
        bool hasExisting = _sprayers.TryGetValue(playerUuid, out SprayerEmitterPair pair);
        if (hasExisting
            && store.GetUuid(pair.NozzleEntity) == pair.NozzleUuid
            && store.GetUuid(pair.ImpactEntity) == pair.ImpactUuid)
        {
            _seenPlayers.Add(playerUuid);
            UpdateNozzleEmitter(store, pair.NozzleEntity, in nozzleEmitter);
            UpdateImpactEmitter(store, pair.ImpactEntity, in impactPos, hit, in impactEmitter);
            return;
        }

        if (hasExisting)
        {
            DespawnPair(store, in pair);
            _sprayers.Remove(playerUuid);
        }

        Result<int> nozzleResult = _particles.SpawnEmitter(
            store,
            in nozzleEmitter,
            in transform.Position,
            in transform.Orientation,
            parentEntity: playerUuid,
            excludeEntity: playerUuid
        );
        if (!nozzleResult.Success)
        {
            return;
        }

        Result<int> impactResult = _particles.SpawnEmitter(
            store,
            in impactEmitter,
            in impactPos,
            parentEntity: Uuid.Null,
            excludeEntity: playerUuid
        );
        if (!impactResult.Success)
        {
            _particles.DespawnEmitter(store, nozzleResult.Value, store.GetUuid(nozzleResult.Value));
            return;
        }

        _sprayers[playerUuid] = new SprayerEmitterPair(
            nozzleResult.Value,
            store.GetUuid(nozzleResult.Value),
            impactResult.Value,
            store.GetUuid(impactResult.Value)
        );
        _seenPlayers.Add(playerUuid);
    }

    private void ReleaseStaleSprayers(DataStore store)
    {
        if (_sprayers.Count == 0)
        {
            return;
        }

        _stalePlayers.Clear();
        foreach (Uuid playerUuid in _sprayers.Keys)
        {
            if (!_seenPlayers.Contains(playerUuid))
            {
                _stalePlayers.Add(playerUuid);
            }
        }

        for (var i = 0; i < _stalePlayers.Count; i++)
        {
            Uuid playerUuid = _stalePlayers[i];
            if (_sprayers.Remove(playerUuid, out SprayerEmitterPair pair))
            {
                DespawnPair(store, in pair);
            }
        }
    }

    private void DespawnPair(DataStore store, in SprayerEmitterPair pair)
    {
        _particles.DespawnEmitter(store, pair.NozzleEntity, pair.NozzleUuid);
        _particles.DespawnEmitter(store, pair.ImpactEntity, pair.ImpactUuid);
    }

    private static void UpdateNozzleEmitter(DataStore store, int nozzleEntity, in ParticleEmitterComponent candidate)
    {
        if (!store.TryGet(nozzleEntity, out ParticleEmitterComponent existing)
            || HasNozzleEmitterChanged(in existing, in candidate))
        {
            store.AddOrUpdate(nozzleEntity, candidate);
        }
    }

    private static void UpdateImpactEmitter(
        DataStore store,
        int impactEntity,
        in Vector3 impactPos,
        bool hit,
        in ParticleEmitterComponent candidate
    ) {
        if (hit)
        {
            bool transformChanged = !store.TryGet(impactEntity, out TransformComponent existingTransform)
                || Vector3.DistanceSquared(existingTransform.Position, impactPos) > TARGET_DELTA_SQ_THRESHOLD;
            if (transformChanged)
            {
                store.AddOrUpdate(impactEntity, new TransformComponent(impactPos));
            }
        }

        if (!store.TryGet(impactEntity, out ParticleEmitterComponent existing)
            || HasImpactEmitterChanged(in existing, in candidate))
        {
            store.AddOrUpdate(impactEntity, candidate);
        }
    }

    private static InputComponent ResolveInput(in NetworkComponent net, in InputComponent latestInput, uint simTick, uint maxAgeTicks)
    {
        //  An empty ring has no staged samples yet, so the live component is the best available source.
        //  Once samples are staged, a stale or future-only ring resolves as idle instead of falling back.
        if (net.StagedInputs == null || net.StagedInputs.Count == 0)
        {
            return latestInput;
        }

        Result<InputComponent> staged = net.StagedInputs.GetCommand(simTick, maxAgeTicks);
        return staged.Success ? staged.Value : default;
    }

    private bool IsHoldingSprayerTool(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out EquipmentComponent equipment)
            || !store.TryGet(entity, out InventoryComponent inventory))
        {
            return false;
        }

        ItemData itemData = inventory.Get(new SlotId(SlotGroups.HOTBAR, equipment.ActiveInventorySlot));
        if (itemData.IsEmpty())
        {
            return false;
        }

        Result<Item> itemResult = _itemDatabase.Get(itemData.ID);
        return itemResult.Success && itemResult.Value.Tool?.Type == ToolType.Sprayer;
    }

    private uint ResolveColorTint(string? requestedPaintId)
    {
        if (_paintDatabase == null)
        {
            return 0u;
        }

        if (!string.IsNullOrEmpty(requestedPaintId))
        {
            Result<Paint> requestedResult = _paintDatabase.Get(requestedPaintId);
            if (requestedResult.Success && requestedResult.Value.Kind == PaintKind.Color)
            {
                return ParticleEffect.PackTint(in requestedResult.Value.Color);
            }
        }

        IReadOnlyList<Paint> colors = _paintDatabase.ColorPaints;
        return colors.Count > 0 ? ParticleEffect.PackTint(in colors[0].Color) : 0u;
    }

    private static bool HasNozzleEmitterChanged(in ParticleEmitterComponent current, in ParticleEmitterComponent next)
    {
        if (current.Playing != next.Playing
            || current.TintRgba != next.TintRgba
            || !string.Equals(current.EffectId, next.EffectId, StringComparison.Ordinal))
        {
            return true;
        }

        if (Vector3.DistanceSquared(current.Offset, next.Offset) > TARGET_DELTA_SQ_THRESHOLD)
        {
            return true;
        }

        return Vector3.DistanceSquared(current.LocalDirection, next.LocalDirection) > NORMAL_DELTA_SQ_THRESHOLD;
    }

    private static bool HasImpactEmitterChanged(in ParticleEmitterComponent current, in ParticleEmitterComponent next)
    {
        if (current.Playing != next.Playing
            || current.TintRgba != next.TintRgba
            || !string.Equals(current.EffectId, next.EffectId, StringComparison.Ordinal))
        {
            return true;
        }

        return next.Playing && Vector3.DistanceSquared(current.LocalDirection, next.LocalDirection) > NORMAL_DELTA_SQ_THRESHOLD;
    }

    private readonly record struct SprayerEmitterPair(
        int NozzleEntity,
        Uuid NozzleUuid,
        int ImpactEntity,
        Uuid ImpactUuid
    );

    private struct UpdateAction : IForEach<NetworkComponent, OwnedCharacterComponent, TransformComponent, InputComponent>
    {
        public ServerPaintSprayerSystem Owner;
        public uint SimTick;

        public void Execute(
            float delta,
            DataStore store,
            int entity,
            in NetworkComponent net,
            in OwnedCharacterComponent owned,
            in TransformComponent transform,
            in InputComponent input
        ) {
            Owner.UpdatePlayerSprayer(store, entity, SimTick, in net, in transform, in input);
        }
    }
}
