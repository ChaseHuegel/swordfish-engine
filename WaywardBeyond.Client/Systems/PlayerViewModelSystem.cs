using System;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Items;
using WaywardBeyond.Meshing;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Systems;

/// <summary>Maintains the first-person view model entity for the player's held item.</summary>
internal sealed class PlayerViewModelSystem(
    in PlayerData playerData,
    in IAssetDatabase<Material> materialDatabase,
    in IAssetDatabase<Mesh> meshDatabase,
    in IBrickDatabase brickDatabase,
    in BrickPreviewMeshBuilder brickMeshBuilder,
    in VoxelMaterials voxelMaterials,
    in InteractionState interactionState,
    in IInputService inputService,
    IBundledBlueprintDatabase? bundledBlueprints = null,
    IBrickRegistry? brickRegistry = null,
    RegionMeshBuilder? regionMeshBuilder = null,
    IPaintRegistry? paintRegistry = null
) : IEntitySystem, IDisposable {
    private static readonly ViewModelAnimationProfile _drillAnimation = new(
        TriggerOnSecondary: true,
        PositionAmplitude: new Vector3(0.005f, 0.005f, 0.007f),
        PositionFrequency: new Vector3(67f, 83f, 59f),
        YawAmplitude: 0f,
        YawFrequency: 0f,
        PitchAmplitude: 0.008f,
        PitchFrequency: 61f,
        RollAmplitude: 0.014f,
        RollFrequency: 73f
    );

    private static readonly ViewModelAnimationProfile _miningLaserAnimation = new(
        TriggerOnSecondary: false,
        PositionAmplitude: new Vector3(0.0024f, 0.002f, 0.003f),
        PositionFrequency: new Vector3(27f, 39f, 17f),
        YawAmplitude: 0.003f,
        YawFrequency: 53f,
        PitchAmplitude: 0.004f,
        PitchFrequency: 23f,
        RollAmplitude: 0.0045f,
        RollFrequency: 41f
    );

    private static readonly ModelDefinition _defaultViewModel = new()
    {
        Position = new ModelDefinition.Float3(0.4f, -0.4f, -0.5f),
        Rotation = new ModelDefinition.Float3(0f, -60f, 0f),
        Scale = new ModelDefinition.Float3(0.25f, 0.25f, 0.25f),
    };

    private readonly PlayerData _playerData = playerData;
    private readonly IAssetDatabase<Material> _materialDatabase = materialDatabase;
    private readonly IAssetDatabase<Mesh> _meshDatabase = meshDatabase;
    private readonly IBrickDatabase _brickDatabase = brickDatabase;
    private readonly BrickPreviewMeshBuilder _brickMeshBuilder = brickMeshBuilder;
    private readonly VoxelMaterials _voxelMaterials = voxelMaterials;
    private readonly InteractionState _interactionState = interactionState;
    private readonly IInputService _inputService = inputService;
    private readonly IBundledBlueprintDatabase? _bundledBlueprints = bundledBlueprints;
    private readonly IBrickRegistry? _brickRegistry = brickRegistry;
    private readonly RegionMeshBuilder? _regionMeshBuilder = regionMeshBuilder;
    private readonly IPaintRegistry? _paintRegistry = paintRegistry;

    private Entity? _viewModelEntity;
    private ViewModelKey? _currentKey;
    private ChildComponent _restingChild;
    private double _animationTime;
    private bool _isAnimating;

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        //  The view model entity is owned by this system, not by a gameplay tag, so the tag-based world
        //  teardown never frees it. Free it here once play ends (menu or loading) and rebuild it on the
        //  next join.
        if (_viewModelEntity.HasValue && !WaywardBeyond.IsInGame())
        {
            FreeViewModel(store);
        }

        ForEachAction action = new() { Owner = this };
        store.Query<PlayerComponent, InventoryComponent, ForEachAction>(delta, ref action);
    }

    private BrickShape ResolveShape(Item item)
    {
        if (item.Placeable is not { Type: PlaceableType.Brick } placeable)
        {
            return BrickShape.Block;
        }

        Result<Brick> brickResult = _brickDatabase.Get(placeable.ID);
        if (!brickResult.Success)
        {
            return BrickShape.Block;
        }

        Brick brick = brickResult.Value;
        BrickShape shape = brick.Shapeable ? _interactionState.SelectedShape.Get() : brick.Shape;
        return shape == BrickShape.Any ? BrickShape.Block : shape;
    }

    private ResolvedVisuals ResolveVisuals(Item item, in ModelDefinition viewModel, BrickShape shape)
    {
        if (!string.IsNullOrEmpty(viewModel.Mesh) && !string.IsNullOrEmpty(viewModel.Material))
        {
            Result<Mesh> meshResult = _meshDatabase.Get(viewModel.Mesh);
            Result<Material> materialResult = _materialDatabase.Get(viewModel.Material);
            if (meshResult.Success && materialResult.Success)
            {
                var options = new RenderOptions
                {
                    CastShadows = false,
                    ReceiveShadows = true,
                };
                return new ResolvedVisuals(meshResult.Value, materialResult.Value, options);
            }
        }

        if (item.Placeable is { Type: PlaceableType.Brick } placeable)
        {
            Result<Brick> brickResult = _brickDatabase.Get(placeable.ID);
            if (brickResult.Success)
            {
                Brick brick = brickResult.Value;
                Result<VoxelMesh> brickMesh = _brickMeshBuilder.Build(brick, shape, Orientation.Identity);
                if (brickMesh.Success)
                {
                    RenderOptions options = _voxelMaterials.GetRenderOptions(brick.Alpha);
                    options.CastShadows = false;
                    options.ReceiveShadows = true;
                    return new ResolvedVisuals(brickMesh.Value.ToEngineMesh(), _voxelMaterials.GetMaterial(brick.Alpha), options);
                }
            }
        }

        if (item.Placeable is { Type: PlaceableType.Blueprint } blueprintPlaceable
            && _bundledBlueprints != null
            && _brickRegistry != null
            && _regionMeshBuilder != null)
        {
            Result<BlueprintData> blueprintResult = _bundledBlueprints.Get(blueprintPlaceable.ID);
            if (blueprintResult.Success)
            {
                VoxelRegionData encoded = blueprintResult.Value.ToRegion();
                VoxelRegionData decoded = VoxelCodec.Decode(in encoded, _brickRegistry, _paintRegistry);
                Result<VoxelMesh> regionMesh = _regionMeshBuilder.Build(in decoded);
                if (regionMesh.Success)
                {
                    VoxelMesh normalized = RegionMeshBuilder.NormalizeToUnitCube(in regionMesh.Value);
                    BrickAlpha alpha = RegionMeshBuilder.ResolveAlphaMode(in decoded, _brickDatabase);
                    RenderOptions options = _voxelMaterials.GetRenderOptions(alpha);
                    options.CastShadows = false;
                    options.ReceiveShadows = true;
                    return new ResolvedVisuals(normalized.ToEngineMesh(), _voxelMaterials.GetMaterial(alpha), options);
                }
            }
        }

        Mesh fallbackMesh = _brickMeshBuilder.BuildFallback().ToEngineMesh();
        RenderOptions fallbackOptions = _voxelMaterials.OpaqueOptions;
        fallbackOptions.CastShadows = false;
        fallbackOptions.ReceiveShadows = true;
        return new ResolvedVisuals(fallbackMesh, _voxelMaterials.Opaque, fallbackOptions);
    }

    private void ClearRenderer()
    {
        if (_viewModelEntity.HasValue)
        {
            MeshRendererComponent? meshRendererComponent = _viewModelEntity.Value.Get<MeshRendererComponent>();
            if (meshRendererComponent != null)
            {
                //  The engine retires the renderer and any mesh it owns once its component is gone.
                _viewModelEntity.Value.Remove<MeshRendererComponent>();
            }
        }
    }

    private bool TryGetActiveAnimation(Item item, out ViewModelAnimationProfile profile)
    {
        if (_interactionState.IsInteractionBlocked() || item.Tool == null)
        {
            profile = default;
            return false;
        }

        switch (item.Tool.Value.Type)
        {
            case ToolType.Drill:
                profile = _drillAnimation;
                return profile.IsTriggered(_inputService);
            case ToolType.Mining:
                profile = _miningLaserAnimation;
                return profile.IsTriggered(_inputService);
            default:
                profile = default;
                return false;
        }
    }

    private void UpdateToolAnimation(float delta, Item item)
    {
        if (!TryGetActiveAnimation(item, out ViewModelAnimationProfile profile))
        {
            ResetAnimation();
            return;
        }

        if (!_viewModelEntity.HasValue || !float.IsFinite(delta) || delta <= 0f)
        {
            return;
        }

        _animationTime = MathS.Repeat(_animationTime + delta, MathS.TIME_WRAP_SECONDS);
        ChildComponent animated = profile.Evaluate(in _restingChild, (float)_animationTime);
        _viewModelEntity.Value.AddOrUpdate(animated);
        _isAnimating = true;
    }

    private void ResetAnimation()
    {
        _animationTime = 0d;
        if (!_isAnimating)
        {
            return;
        }

        _isAnimating = false;
        if (_viewModelEntity.HasValue)
        {
            _viewModelEntity.Value.AddOrUpdate(_restingChild);
        }
    }

    private static ChildComponent CreateRestingChild(in Uuid parent, in ModelDefinition viewModel)
    {
        return new ChildComponent(in parent)
        {
            LocalPosition = new Vector3(viewModel.Position.X, viewModel.Position.Y, viewModel.Position.Z),
            LocalOrientation = Quaternion.CreateFromYawPitchRoll(
                viewModel.Rotation.Y * MathS.DEGREES_TO_RADIANS,
                viewModel.Rotation.X * MathS.DEGREES_TO_RADIANS,
                viewModel.Rotation.Z * MathS.DEGREES_TO_RADIANS
            ),
            LocalScale = new Vector3(viewModel.Scale.X, viewModel.Scale.Y, viewModel.Scale.Z),
        };
    }

    private void FreeViewModel(DataStore store)
    {
        if (_viewModelEntity.HasValue)
        {
            store.Free(_viewModelEntity.Value.Ptr);
        }

        _viewModelEntity = null;
        _currentKey = null;
        _restingChild = default;
        _animationTime = 0d;
        _isAnimating = false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _viewModelEntity = null;
        _currentKey = null;
        _restingChild = default;
        _animationTime = 0d;
        _isAnimating = false;
    }
    
    private struct ForEachAction : IForEach<PlayerComponent, InventoryComponent>
    {
        public PlayerViewModelSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player, in InventoryComponent inventory)
        {
            if (!Owner._viewModelEntity.HasValue)
            {
                Owner._viewModelEntity = new Entity(store.Alloc(), store);
                Owner._viewModelEntity.Value.AddOrUpdate(new IdentifierComponent("PlayerViewModel"));
                Owner._viewModelEntity.Value.AddOrUpdate(new ViewModelComponent());
                Owner._viewModelEntity.Value.AddOrUpdate(new TransformComponent());
            }

            Result<ItemSlot> mainHandResult = Owner._playerData.GetMainHand(store, entity, inventory);
            if (!mainHandResult.Success || !WaywardBeyond.IsPlaying())
            {
                Owner.ResetAnimation();
                if (Owner._currentKey != null)
                {
                    Owner._currentKey = null;
                    Owner.ClearRenderer();
                }

                return;
            }

            Item item = mainHandResult.Value.Item;
            ModelDefinition viewModel = item.ViewModel ?? _defaultViewModel;
            BrickShape shape = Owner.ResolveShape(item);
            Uuid playerUuid = store.GetUuid(entity);
            var key = new ViewModelKey(playerUuid, item.ID, viewModel, shape);

            if (!key.Equals(Owner._currentKey))
            {
                Owner._currentKey = key;
                Owner._restingChild = CreateRestingChild(in playerUuid, in viewModel);
                Owner._isAnimating = false;
                Owner._animationTime = 0d;
                Owner.ClearRenderer();

                ResolvedVisuals visuals = Owner.ResolveVisuals(item, viewModel, shape);

                var meshRenderer = new MeshRenderer(visuals.Mesh, visuals.Material, visuals.RenderOptions);
                Owner._viewModelEntity.Value.AddOrUpdate(new MeshRendererComponent(meshRenderer));
                Owner._viewModelEntity.Value.AddOrUpdate(Owner._restingChild);
            }

            Owner.UpdateToolAnimation(delta, item);
        }
    }
    
    private readonly record struct ViewModelKey(Uuid PlayerUuid, string ItemID, ModelDefinition Model, BrickShape Shape);

    private readonly record struct ViewModelAnimationProfile(
        bool TriggerOnSecondary,
        Vector3 PositionAmplitude,
        Vector3 PositionFrequency,
        float YawAmplitude,
        float YawFrequency,
        float PitchAmplitude,
        float PitchFrequency,
        float RollAmplitude,
        float RollFrequency
    ) {
        public bool IsTriggered(IInputService inputService) =>
            inputService.IsMouseHeld(MouseButton.Left)
            || (TriggerOnSecondary && inputService.IsMouseHeld(MouseButton.Right));

        public ChildComponent Evaluate(in ChildComponent resting, float time)
        {
            var positionOffset = new Vector3(
                MathF.Sin(time * PositionFrequency.X) * PositionAmplitude.X,
                MathF.Sin(time * PositionFrequency.Y) * PositionAmplitude.Y,
                MathF.Sin(time * PositionFrequency.Z) * PositionAmplitude.Z
            );
            
            var rotationOffset = Quaternion.CreateFromYawPitchRoll(
                yaw: MathF.Sin(time * YawFrequency) * YawAmplitude,
                pitch: MathF.Sin(time * PitchFrequency) * PitchAmplitude,
                roll: MathF.Sin(time * RollFrequency) * RollAmplitude
            );
            
            return new ChildComponent(resting.Parent)
            {
                LocalPosition = resting.LocalPosition + positionOffset,
                LocalOrientation = Quaternion.Normalize(resting.LocalOrientation * rotationOffset),
                LocalScale = resting.LocalScale,
            };
        }
    }

    private readonly record struct ResolvedVisuals(
        Mesh Mesh,
        Material Material,
        RenderOptions RenderOptions
    );
}
