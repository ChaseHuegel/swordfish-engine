using System;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.Particles;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.Physics;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Renders local predicted and remote replicated mining laser beams, impact lights, and continuous
/// surface impact particle emitters.
/// </summary>
internal sealed class MiningLaserEffectSystem(
    in PlayerData playerData,
    in InteractionState interactionState,
    in IInputService inputService,
    in IPhysics physics,
    in IAssetDatabase<Material> materialDatabase,
    in IAssetDatabase<ParticleEffect> particleEffectDatabase
) : IEntitySystem, IDisposable
{
    private const string BEAM_MATERIAL_ID = "effects/laser_beam";
    private const string IMPACT_EFFECT_ID = "mining_laser_impact";
    private const float IMPACT_LIGHT_RADIUS = 1.5f;
    private const float IMPACT_LIGHT_SIZE = 0.15f;
    private const float IMPACT_LIGHT_OFFSET = 0.08f;
    private const float IMPACT_LIGHT_BASE_INTENSITY = 4.0f;
    private const float IMPACT_LIGHT_FLICKER_AMPLITUDE = 0.6f;
    private const float IMPACT_LIGHT_FLICKER_FREQUENCY = 53f;
    private const float BEAM_PULSE_AMPLITUDE = 0.14f;
    private const float BEAM_PULSE_FREQUENCY = 48f;
    private const float MIN_BEAM_LENGTH = 0.001f;

    private static readonly Vector3 _impactLightColor = new(254f / 255f, 174f / 255f, 52f / 255f);
    private static readonly RenderOptions _beamRenderOptions = new()
    {
        DoubleFaced = false,
        CastShadows = false,
        ReceiveShadows = false,
        AlphaMode = AlphaMode.Blend,
    };

    private readonly PlayerData _playerData = playerData;
    private readonly InteractionState _interactionState = interactionState;
    private readonly IInputService _inputService = inputService;
    private readonly IPhysics _physics = physics;
    private readonly IAssetDatabase<Material> _materialDatabase = materialDatabase;
    private readonly IAssetDatabase<ParticleEffect> _particleEffectDatabase = particleEffectDatabase;

    private readonly LaserBeamMesh _beamMesh = new();
    private readonly Dictionary<Uuid, LaserBeamVisual> _beams = [];
    private readonly HashSet<Uuid> _seenOwners = [];
    private readonly List<Uuid> _staleOwners = [];

    private Material? _beamMaterial;
    private ParticleEffect? _impactEffect;
    private bool _assetsResolved;
    private float _time;

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        if (!WaywardBeyond.IsInGame())
        {
            ReleaseAll(store);
            return;
        }

        EnsureAssets();

        if (float.IsFinite(delta) && delta > 0f)
        {
            _time += delta;
        }

        _seenOwners.Clear();

        LocalLaserAction localAction = new() { Owner = this };
        store.Query<PlayerComponent, TransformComponent, InventoryComponent, LocalLaserAction>(delta, ref localAction);

        RemoteLaserAction remoteAction = new() { Owner = this };
        store.Query<BodyViewComponent, TransformComponent, LaserBeamComponent, RemoteLaserAction>(delta, ref remoteAction);

        ReleaseStaleBeams(store);
    }

    private void EnsureAssets()
    {
        if (_assetsResolved)
        {
            return;
        }

        _assetsResolved = true;

        Result<Material> materialResult = _materialDatabase.Get(BEAM_MATERIAL_ID);
        if (materialResult.Success)
        {
            _beamMaterial = materialResult.Value;
        }

        Result<ParticleEffect> effectResult = _particleEffectDatabase.Get(IMPACT_EFFECT_ID);
        if (effectResult.Success)
        {
            _impactEffect = effectResult.Value;
        }
    }

    private void UpdateLocalPlayer(
        DataStore store,
        int entity,
        in TransformComponent transform,
        in InventoryComponent inventory
    ) {
        if (_interactionState.IsInteractionBlocked() || !_inputService.IsMouseHeld(MouseButton.Left))
        {
            return;
        }

        Result<ItemSlot> mainHandResult = _playerData.GetMainHand(store, entity, inventory);
        if (!mainHandResult.Success || mainHandResult.Value.Item.Tool?.Type != ToolType.Mining)
        {
            return;
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

        Vector3 muzzle = LaserBeamGeometry.ComputeFirstPersonMuzzle(in transform, mainHandResult.Value.Item.ViewModel);
        Vector3 start = ToolAimGeometry.ComputeSafeStart(in transform, muzzle, target);

        AddOrUpdateBeam(store, store.GetUuid(entity), start, target, normal, hit);
    }

    private void UpdateRemotePlayer(
        DataStore store,
        int entity,
        in TransformComponent transform,
        in LaserBeamComponent beam
    ) {
        if (store.Has(typeof(PlayerComponent), entity))
        {
            return;
        }

        Vector3 start = ToolAimGeometry.ComputeRemoteOrigin(in transform);
        AddOrUpdateBeam(store, store.GetUuid(entity), start, beam.Target, beam.Normal, beam.Hit);
    }

    private void AddOrUpdateBeam(
        DataStore store,
        Uuid owner,
        Vector3 start,
        Vector3 target,
        Vector3 normal,
        bool hit
    ) {
        if (!ToolAimGeometry.IsFinite(in start) || !ToolAimGeometry.IsFinite(in target) || !ToolAimGeometry.IsFinite(in normal))
        {
            return;
        }

        _seenOwners.Add(owner);
        LaserBeamVisual visual = GetOrCreateBeam(store, owner);

        Vector3 beamDelta = target - start;
        float length = beamDelta.Length();
        if (length > MIN_BEAM_LENGTH)
        {
            float pulse = 1.0f + BEAM_PULSE_AMPLITUDE * MathF.Sin(_time * BEAM_PULSE_FREQUENCY);
            Quaternion orientation = LaserBeamGeometry.CreateBeamOrientation(beamDelta / length);
            store.AddOrUpdate(visual.BeamEntity, new TransformComponent(start, orientation, new Vector3(pulse, pulse, length)));

            if (!visual.HasRenderer && _beamMaterial != null)
            {
                var renderer = new MeshRenderer(_beamMesh, _beamMaterial, _beamRenderOptions);
                store.AddOrUpdate(visual.BeamEntity, new MeshRendererComponent(renderer));
                visual.HasRenderer = true;
            }
        }
        else
        {
            store.AddOrUpdate(visual.BeamEntity, new TransformComponent(start, Quaternion.Identity, Vector3.Zero));
        }

        Vector3 safeNormal = normal.LengthSquared() > 1e-6f ? Vector3.Normalize(normal) : Vector3.UnitY;

        if (hit)
        {
            float intensity = IMPACT_LIGHT_BASE_INTENSITY + IMPACT_LIGHT_FLICKER_AMPLITUDE * MathF.Sin(_time * IMPACT_LIGHT_FLICKER_FREQUENCY);
            store.AddOrUpdate(visual.ImpactLightEntity, new TransformComponent(target + safeNormal * IMPACT_LIGHT_OFFSET));
            store.AddOrUpdate(visual.ImpactLightEntity, new LightComponent(
                radius: IMPACT_LIGHT_RADIUS,
                color: _impactLightColor,
                size: IMPACT_LIGHT_SIZE,
                castShadows: false,
                intensity: intensity
            ));
            visual.HasLight = true;
        }
        else if (visual.HasLight)
        {
            store.Remove(typeof(LightComponent), visual.ImpactLightEntity);
            visual.HasLight = false;
        }

        if (_impactEffect != null)
        {
            store.AddOrUpdate(visual.ImpactEmitterEntity, new TransformComponent(target + safeNormal * ToolAimGeometry.IMPACT_EMITTER_OFFSET));
            store.AddOrUpdate(visual.ImpactEmitterEntity, new ParticleEmitterComponent(_impactEffect, playing: hit)
            {
                LocalDirection = safeNormal,
            });
        }
    }

    private LaserBeamVisual GetOrCreateBeam(DataStore store, Uuid owner)
    {
        if (_beams.TryGetValue(owner, out LaserBeamVisual? existing))
        {
            if (store.GetUuid(existing.BeamEntity) == existing.BeamUuid &&
                store.GetUuid(existing.ImpactLightEntity) == existing.ImpactLightUuid &&
                store.GetUuid(existing.ImpactEmitterEntity) == existing.ImpactEmitterUuid)
            {
                return existing;
            }

            FreeBeamEntities(store, existing);
            _beams.Remove(owner);
        }

        int beamEntity = AllocateTaggedEntity(store, out Uuid beamUuid);
        int lightEntity = AllocateTaggedEntity(store, out Uuid lightUuid);
        int emitterEntity = AllocateTaggedEntity(store, out Uuid emitterUuid);

        var visual = new LaserBeamVisual(beamEntity, beamUuid, lightEntity, lightUuid, emitterEntity, emitterUuid);
        _beams[owner] = visual;
        return visual;
    }

    private static int AllocateTaggedEntity(DataStore store, out Uuid uuid)
    {
        int entity = store.Alloc();
        uuid = store.GetUuid(entity);
        store.AddOrUpdate(entity, new IdentifierComponent(name: null, tag: "game"));
        store.AddOrUpdate(entity, new TransformComponent());
        return entity;
    }

    private void ReleaseStaleBeams(DataStore store)
    {
        if (_beams.Count == 0)
        {
            return;
        }

        _staleOwners.Clear();
        foreach (Uuid owner in _beams.Keys)
        {
            if (!_seenOwners.Contains(owner))
            {
                _staleOwners.Add(owner);
            }
        }

        for (var i = 0; i < _staleOwners.Count; i++)
        {
            Uuid owner = _staleOwners[i];
            if (_beams.Remove(owner, out LaserBeamVisual? visual))
            {
                FreeBeamEntities(store, visual);
            }
        }
    }

    private void ReleaseAll(DataStore store)
    {
        if (_beams.Count == 0)
        {
            return;
        }

        foreach (LaserBeamVisual visual in _beams.Values)
        {
            FreeBeamEntities(store, visual);
        }

        _beams.Clear();
    }

    private static void FreeBeamEntities(DataStore store, LaserBeamVisual visual)
    {
        FreeIfOwned(store, visual.BeamEntity, visual.BeamUuid);
        FreeIfOwned(store, visual.ImpactLightEntity, visual.ImpactLightUuid);
        FreeIfOwned(store, visual.ImpactEmitterEntity, visual.ImpactEmitterUuid);
    }

    private static void FreeIfOwned(DataStore store, int entity, Uuid uuid)
    {
        if (entity != Entity.Null && store.GetUuid(entity) == uuid)
        {
            store.Free(entity);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _beams.Clear();
        _beamMesh.Dispose();
    }

    private sealed class LaserBeamVisual(
        int beamEntity,
        Uuid beamUuid,
        int impactLightEntity,
        Uuid impactLightUuid,
        int impactEmitterEntity,
        Uuid impactEmitterUuid
    ) {
        public readonly int BeamEntity = beamEntity;
        public readonly Uuid BeamUuid = beamUuid;
        public readonly int ImpactLightEntity = impactLightEntity;
        public readonly Uuid ImpactLightUuid = impactLightUuid;
        public readonly int ImpactEmitterEntity = impactEmitterEntity;
        public readonly Uuid ImpactEmitterUuid = impactEmitterUuid;
        public bool HasRenderer;
        public bool HasLight;
    }

    private struct LocalLaserAction : IForEach<PlayerComponent, TransformComponent, InventoryComponent>
    {
        public MiningLaserEffectSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player, in TransformComponent transform, in InventoryComponent inventory)
        {
            Owner.UpdateLocalPlayer(store, entity, in transform, in inventory);
        }
    }

    private struct RemoteLaserAction : IForEach<BodyViewComponent, TransformComponent, LaserBeamComponent>
    {
        public MiningLaserEffectSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in BodyViewComponent body, in TransformComponent transform, in LaserBeamComponent beam)
        {
            Owner.UpdateRemotePlayer(store, entity, in transform, in beam);
        }
    }
}
