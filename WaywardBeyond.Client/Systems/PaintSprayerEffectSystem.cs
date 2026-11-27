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
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Drives local predicted first-person nozzle aerosol streams and surface overspray mist while the local
/// player holds primary or secondary fire with a paint sprayer equipped.
/// </summary>
internal sealed class PaintSprayerEffectSystem(
    in PlayerData playerData,
    in InteractionState interactionState,
    in IInputService inputService,
    in IPhysics physics,
    IPaintDatabase? paintDatabase = null
) : IEntitySystem
{
    private const string STREAM_EFFECT_ID = "paint_sprayer_stream";
    private const string IMPACT_EFFECT_ID = "paint_sprayer_impact";
    private const string SOLVENT_STREAM_EFFECT_ID = "paint_sprayer_solvent_stream";
    private const string SOLVENT_IMPACT_EFFECT_ID = "paint_sprayer_solvent_impact";

    private readonly PlayerData _playerData = playerData;
    private readonly InteractionState _interactionState = interactionState;
    private readonly IInputService _inputService = inputService;
    private readonly IPhysics _physics = physics;
    private readonly IPaintDatabase? _paintDatabase = paintDatabase;

    private readonly Dictionary<Uuid, SprayerEmitterVisual> _sprayers = [];
    private readonly HashSet<Uuid> _seenOwners = [];
    private readonly List<Uuid> _staleOwners = [];

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        if (!WaywardBeyond.IsPlaying())
        {
            ReleaseAll(store);
            return;
        }

        _seenOwners.Clear();

        if (!_interactionState.IsInteractionBlocked()
            && (_inputService.IsMouseHeld(MouseButton.Left) || _inputService.IsMouseHeld(MouseButton.Right)))
        {
            FindViewModelChildAction viewModelAction = default;
            store.Query<ViewModelComponent, MeshRendererComponent, ChildComponent, FindViewModelChildAction>(0f, ref viewModelAction);

            LocalSprayerAction action = new()
            {
                Owner = this,
                HasViewModelChild = viewModelAction.Found,
                ViewModelChild = viewModelAction.Child,
            };
            store.Query<PlayerComponent, TransformComponent, InventoryComponent, LocalSprayerAction>(delta, ref action);
        }

        ReleaseStaleSprayers(store);
    }

    private void UpdateLocalPlayer(
        DataStore store,
        int entity,
        in TransformComponent transform,
        in InventoryComponent inventory,
        bool hasViewModelChild,
        in ChildComponent viewModelChild
    ) {
        if (_interactionState.IsInteractionBlocked())
        {
            return;
        }

        bool primaryHeld = _inputService.IsMouseHeld(MouseButton.Left);
        bool secondaryHeld = _inputService.IsMouseHeld(MouseButton.Right);
        if (!primaryHeld && !secondaryHeld)
        {
            return;
        }

        Result<ItemSlot> mainHandResult = _playerData.GetMainHand(store, entity, inventory);
        if (!mainHandResult.Success || mainHandResult.Value.Item.Tool?.Type != ToolType.Sprayer)
        {
            return;
        }

        string streamEffectId;
        string impactEffectId;
        uint tintRgba;
        if (primaryHeld)
        {
            Paint? activeColor = ResolveActiveColorPaint();
            streamEffectId = STREAM_EFFECT_ID;
            impactEffectId = IMPACT_EFFECT_ID;
            tintRgba = activeColor != null ? ParticleEffect.PackTint(in activeColor.Color) : 0u;
        }
        else
        {
            streamEffectId = SOLVENT_STREAM_EFFECT_ID;
            impactEffectId = SOLVENT_IMPACT_EFFECT_ID;
            tintRgba = 0u;
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

        Uuid owner = store.GetUuid(entity);
        Vector3 nozzle = hasViewModelChild && viewModelChild.Parent == owner
            ? PaintSprayerGeometry.ComputeFirstPersonNozzle(in transform, in viewModelChild)
            : PaintSprayerGeometry.ComputeFirstPersonNozzle(in transform, mainHandResult.Value.Item.ViewModel);
        Vector3 start = ToolAimGeometry.ComputeSafeStart(in transform, nozzle, target);
        Vector3 sprayDir = ToolAimGeometry.ComputeAimDirection(in transform, start, target);

        AddOrUpdateSprayer(
            store,
            owner,
            start,
            sprayDir,
            target,
            normal,
            hit,
            streamEffectId,
            impactEffectId,
            tintRgba
        );
    }

    private Paint? ResolveActiveColorPaint()
    {
        if (_paintDatabase == null)
        {
            return null;
        }

        string? paintId = _interactionState.SelectedColorPaintId.Get();
        if (!string.IsNullOrEmpty(paintId))
        {
            Result<Paint> selectedResult = _paintDatabase.Get(paintId);
            if (selectedResult.Success && selectedResult.Value.Kind == PaintKind.Color)
            {
                return selectedResult.Value;
            }
        }

        IReadOnlyList<Paint> colors = _paintDatabase.ColorPaints;
        return colors.Count > 0 ? colors[0] : null;
    }

    private void AddOrUpdateSprayer(
        DataStore store,
        Uuid owner,
        Vector3 start,
        Vector3 sprayDir,
        Vector3 target,
        Vector3 normal,
        bool hit,
        string streamEffectId,
        string impactEffectId,
        uint tintRgba
    ) {
        if (!ToolAimGeometry.IsFinite(in start) ||
            !ToolAimGeometry.IsFinite(in sprayDir) ||
            !ToolAimGeometry.IsFinite(in target) ||
            !ToolAimGeometry.IsFinite(in normal))
        {
            return;
        }

        _seenOwners.Add(owner);
        SprayerEmitterVisual visual = GetOrCreateSprayer(store, owner);

        store.AddOrUpdate(visual.NozzleEmitterEntity, new TransformComponent(start));
        store.AddOrUpdate(visual.NozzleEmitterEntity, new ParticleEmitterComponent(streamEffectId, playing: true, tintRgba: tintRgba)
        {
            LocalDirection = sprayDir,
        });

        Vector3 safeNormal = normal.LengthSquared() > 1e-6f ? Vector3.Normalize(normal) : Vector3.UnitY;
        store.AddOrUpdate(visual.ImpactEmitterEntity, new TransformComponent(target + safeNormal * ToolAimGeometry.IMPACT_EMITTER_OFFSET));
        store.AddOrUpdate(visual.ImpactEmitterEntity, new ParticleEmitterComponent(impactEffectId, playing: hit, tintRgba: tintRgba)
        {
            LocalDirection = safeNormal,
        });
    }

    private SprayerEmitterVisual GetOrCreateSprayer(DataStore store, Uuid owner)
    {
        if (_sprayers.TryGetValue(owner, out SprayerEmitterVisual? existing))
        {
            if (store.GetUuid(existing.NozzleEmitterEntity) == existing.NozzleEmitterUuid &&
                store.GetUuid(existing.ImpactEmitterEntity) == existing.ImpactEmitterUuid)
            {
                return existing;
            }

            FreeSprayerEntities(store, existing);
            _sprayers.Remove(owner);
        }

        int nozzleEntity = AllocateTaggedEntity(store, out Uuid nozzleUuid);
        int impactEntity = AllocateTaggedEntity(store, out Uuid impactUuid);
        var visual = new SprayerEmitterVisual(nozzleEntity, nozzleUuid, impactEntity, impactUuid);
        _sprayers[owner] = visual;
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

    private void ReleaseStaleSprayers(DataStore store)
    {
        if (_sprayers.Count == 0)
        {
            return;
        }

        _staleOwners.Clear();
        foreach (Uuid owner in _sprayers.Keys)
        {
            if (!_seenOwners.Contains(owner))
            {
                _staleOwners.Add(owner);
            }
        }

        for (var i = 0; i < _staleOwners.Count; i++)
        {
            Uuid owner = _staleOwners[i];
            if (_sprayers.Remove(owner, out SprayerEmitterVisual? visual))
            {
                FreeSprayerEntities(store, visual);
            }
        }
    }

    private void ReleaseAll(DataStore store)
    {
        if (_sprayers.Count == 0)
        {
            return;
        }

        foreach (SprayerEmitterVisual visual in _sprayers.Values)
        {
            FreeSprayerEntities(store, visual);
        }

        _sprayers.Clear();
    }

    private static void FreeSprayerEntities(DataStore store, SprayerEmitterVisual visual)
    {
        FreeIfOwned(store, visual.NozzleEmitterEntity, visual.NozzleEmitterUuid);
        FreeIfOwned(store, visual.ImpactEmitterEntity, visual.ImpactEmitterUuid);
    }

    private static void FreeIfOwned(DataStore store, int entity, Uuid uuid)
    {
        if (entity != Entity.Null && store.GetUuid(entity) == uuid)
        {
            store.Free(entity);
        }
    }

    private sealed class SprayerEmitterVisual(
        int nozzleEmitterEntity,
        Uuid nozzleEmitterUuid,
        int impactEmitterEntity,
        Uuid impactEmitterUuid
    ) {
        public readonly int NozzleEmitterEntity = nozzleEmitterEntity;
        public readonly Uuid NozzleEmitterUuid = nozzleEmitterUuid;
        public readonly int ImpactEmitterEntity = impactEmitterEntity;
        public readonly Uuid ImpactEmitterUuid = impactEmitterUuid;
    }

    private struct FindViewModelChildAction : IForEach<ViewModelComponent, MeshRendererComponent, ChildComponent>
    {
        public bool Found;
        public ChildComponent Child;

        public void Execute(float delta, DataStore store, int entity, in ViewModelComponent viewModel, in MeshRendererComponent renderer, in ChildComponent child)
        {
            Found = true;
            Child = child;
        }
    }

    private struct LocalSprayerAction : IForEach<PlayerComponent, TransformComponent, InventoryComponent>
    {
        public PaintSprayerEffectSystem Owner;
        public bool HasViewModelChild;
        public ChildComponent ViewModelChild;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player, in TransformComponent transform, in InventoryComponent inventory)
        {
            Owner.UpdateLocalPlayer(store, entity, in transform, in inventory, HasViewModelChild, in ViewModelChild);
        }
    }
}
