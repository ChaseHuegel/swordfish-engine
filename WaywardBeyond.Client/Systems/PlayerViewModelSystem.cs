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
    in IInputService inputService
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

    private Entity? _viewModelEntity;
    private Uuid _viewModelUuid;
    private ViewModelKey? _currentKey;
    private ChildComponent _restingChild;
    private double _animationTime;
    private bool _isAnimating;
    private ViewModelReactiveMotion _motion;

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

        if (!action.VisitedPlayer)
        {
            ResetPoseState(store);
        }
    }

    private void EnsureViewModelEntity(DataStore store)
    {
        if (_viewModelEntity.HasValue && store.GetUuid(_viewModelEntity.Value.Ptr) == _viewModelUuid)
        {
            return;
        }

        int viewModelPtr = store.Alloc();
        _viewModelEntity = new Entity(viewModelPtr, store);
        _viewModelUuid = store.GetUuid(viewModelPtr);
        _viewModelEntity.Value.AddOrUpdate(new IdentifierComponent("PlayerViewModel"));
        _viewModelEntity.Value.AddOrUpdate(new ViewModelComponent());
        _viewModelEntity.Value.AddOrUpdate(new TransformComponent());
        _currentKey = null;
        _restingChild = default;
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

    private void UpdateComposedPose(float delta, DataStore store, Item item)
    {
        if (!_viewModelEntity.HasValue)
        {
            return;
        }

        ChildComponent pose = _motion.ApplyTo(in _restingChild);

        if (TryGetActiveAnimation(item, out ViewModelAnimationProfile profile))
        {
            if (float.IsFinite(delta) && delta > 0f)
            {
                _animationTime = MathS.Repeat(_animationTime + delta, MathS.TIME_WRAP_SECONDS);
                pose = profile.Evaluate(in pose, (float)_animationTime);
                _isAnimating = true;
            }
            else if (_isAnimating)
            {
                pose = profile.Evaluate(in pose, (float)_animationTime);
            }
        }
        else
        {
            _animationTime = 0d;
            _isAnimating = false;
        }

        WriteChildIfChanged(store, in pose);
    }

    private void WriteChildIfChanged(DataStore store, in ChildComponent desired)
    {
        int viewModelPtr = _viewModelEntity!.Value.Ptr;
        if (store.TryGet(viewModelPtr, out ChildComponent existing)
            && existing.Parent == desired.Parent
            && existing.LocalPosition == desired.LocalPosition
            && existing.LocalOrientation == desired.LocalOrientation
            && existing.LocalScale == desired.LocalScale)
        {
            return;
        }

        _viewModelEntity.Value.AddOrUpdate(desired);
    }

    private void ResetPoseState(DataStore store)
    {
        _animationTime = 0d;
        _isAnimating = false;
        _motion.Reset();

        if (_viewModelEntity.HasValue
            && store.GetUuid(_viewModelEntity.Value.Ptr) == _viewModelUuid
            && _restingChild.Parent != Uuid.Null)
        {
            WriteChildIfChanged(store, in _restingChild);
        }
    }

    private void FreeViewModel(DataStore store)
    {
        if (_viewModelEntity.HasValue && store.GetUuid(_viewModelEntity.Value.Ptr) == _viewModelUuid)
        {
            store.Free(_viewModelEntity.Value.Ptr);
        }

        _viewModelEntity = null;
        _viewModelUuid = Uuid.Null;
        _currentKey = null;
        _restingChild = default;
        _animationTime = 0d;
        _isAnimating = false;
        _motion.Reset();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _viewModelEntity = null;
        _viewModelUuid = Uuid.Null;
        _currentKey = null;
        _restingChild = default;
        _animationTime = 0d;
        _isAnimating = false;
        _motion.Reset();
    }

    private struct ForEachAction : IForEach<PlayerComponent, InventoryComponent>
    {
        public PlayerViewModelSystem Owner;
        public bool VisitedPlayer;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player, in InventoryComponent inventory)
        {
            VisitedPlayer = true;
            Owner.EnsureViewModelEntity(store);

            Result<ItemSlot> mainHandResult = Owner._playerData.GetMainHand(store, entity, inventory);
            if (!mainHandResult.Success || !WaywardBeyond.IsPlaying())
            {
                Owner.ResetPoseState(store);
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
            bool hasTransform = store.TryGet(entity, out TransformComponent playerTransform);
            bool resetMotion = false;

            if (!key.Equals(Owner._currentKey))
            {
                resetMotion = Owner._currentKey == null
                    || Owner._currentKey.Value.PlayerUuid != playerUuid
                    || !string.Equals(Owner._currentKey.Value.ItemID, item.ID, StringComparison.Ordinal)
                    || !Owner._currentKey.Value.Model.Equals(viewModel);

                Owner._currentKey = key;
                Owner._restingChild = ViewModelGeometry.CreateRestingChild(in playerUuid, in viewModel);
                Owner._isAnimating = false;
                Owner._animationTime = 0d;
                if (resetMotion)
                {
                    Owner._motion.Reset();
                    if (hasTransform)
                    {
                        Owner._motion.SeedOrientation(in playerTransform.Orientation);
                    }
                }

                Owner.ClearRenderer();

                ResolvedVisuals visuals = Owner.ResolveVisuals(item, viewModel, shape);
                var meshRenderer = new MeshRenderer(visuals.Mesh, visuals.Material, visuals.RenderOptions);
                Owner._viewModelEntity!.Value.AddOrUpdate(new MeshRendererComponent(meshRenderer));
            }

            if (!resetMotion)
            {
                bool hasPhysics = store.TryGet(entity, out PhysicsComponent playerPhysics);
                Owner._motion.Update(delta, hasTransform, in playerTransform, hasPhysics, in playerPhysics);
            }

            Owner.UpdateComposedPose(delta, store, item);
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
