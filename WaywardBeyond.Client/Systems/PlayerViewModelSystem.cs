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
    private const float DRILL_VIBRATION_AMPLITUDE_X = 0.005f;
    private const float DRILL_VIBRATION_AMPLITUDE_Y = 0.005f;
    private const float DRILL_VIBRATION_AMPLITUDE_Z = 0.007f;
    private const float DRILL_VIBRATION_PITCH_RADIANS = 0.008f;
    private const float DRILL_VIBRATION_ROLL_RADIANS = 0.014f;
    private const float DRILL_VIBRATION_FREQUENCY_X = 67f;
    private const float DRILL_VIBRATION_FREQUENCY_Y = 83f;
    private const float DRILL_VIBRATION_FREQUENCY_Z = 59f;
    private const float DRILL_VIBRATION_FREQUENCY_PITCH = 61f;
    private const float DRILL_VIBRATION_FREQUENCY_ROLL = 73f;

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
    private ViewModelKey? _currentKey;
    private ChildComponent _restingChild;
    private double _vibrationTime;
    private bool _isVibrating;

    private readonly record struct ViewModelKey(string ItemID, ModelDefinition Model, BrickShape Shape);

    private readonly record struct ResolvedVisuals(
        Mesh Mesh,
        Material Material,
        RenderOptions RenderOptions
    );

    private struct ForEachAction : IForEach<PlayerComponent, InventoryComponent>
    {
        public PlayerViewModelSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player, in InventoryComponent inventory)
        {
            if (!Owner._viewModelEntity.HasValue)
            {
                Owner._viewModelEntity = new Entity(store.Alloc(), store);
                Owner._viewModelEntity.Value.AddOrUpdate(new IdentifierComponent("PlayerViewModel"));
                Owner._viewModelEntity.Value.AddOrUpdate(new TransformComponent());
            }

            Result<ItemSlot> mainHandResult = Owner._playerData.GetMainHand(store, entity, inventory);
            if (!mainHandResult.Success || !WaywardBeyond.IsPlaying())
            {
                Owner.ResetVibration();
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
            var key = new ViewModelKey(item.ID, viewModel, shape);

            if (!key.Equals(Owner._currentKey))
            {
                Owner._currentKey = key;
                Owner._restingChild = CreateRestingChild(store.GetUuid(entity), in viewModel);
                Owner._isVibrating = false;
                Owner._vibrationTime = 0d;
                Owner.ClearRenderer();

                ResolvedVisuals visuals = Owner.ResolveVisuals(item, viewModel, shape);

                var meshRenderer = new MeshRenderer(visuals.Mesh, visuals.Material, visuals.RenderOptions);
                Owner._viewModelEntity.Value.AddOrUpdate(new MeshRendererComponent(meshRenderer));
                Owner._viewModelEntity.Value.AddOrUpdate(Owner._restingChild);
            }

            Owner.UpdateDrillVibration(delta, item);
        }
    }

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

    private bool IsDrillActive(Item item)
    {
        if (item.Tool?.Type != ToolType.Drill || _interactionState.IsInteractionBlocked())
        {
            return false;
        }

        return _inputService.IsMouseHeld(MouseButton.Left) || _inputService.IsMouseHeld(MouseButton.Right);
    }

    private void UpdateDrillVibration(float delta, Item item)
    {
        if (!IsDrillActive(item))
        {
            ResetVibration();
            return;
        }

        if (!_viewModelEntity.HasValue || !float.IsFinite(delta) || delta <= 0f)
        {
            return;
        }

        _vibrationTime = MathS.Repeat(_vibrationTime + delta, MathS.TIME_WRAP_SECONDS);
        ChildComponent animated = ComputeVibratedChild(in _restingChild, (float)_vibrationTime);
        _viewModelEntity.Value.AddOrUpdate(animated);
        _isVibrating = true;
    }

    private void ResetVibration()
    {
        _vibrationTime = 0d;
        if (!_isVibrating)
        {
            return;
        }

        _isVibrating = false;
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

    private static ChildComponent ComputeVibratedChild(in ChildComponent resting, float time)
    {
        var positionOffset = new Vector3(
            MathF.Sin(time * DRILL_VIBRATION_FREQUENCY_X) * DRILL_VIBRATION_AMPLITUDE_X,
            MathF.Sin(time * DRILL_VIBRATION_FREQUENCY_Y) * DRILL_VIBRATION_AMPLITUDE_Y,
            MathF.Sin(time * DRILL_VIBRATION_FREQUENCY_Z) * DRILL_VIBRATION_AMPLITUDE_Z
        );
        var rotationOffset = Quaternion.CreateFromYawPitchRoll(
            yaw: 0f,
            pitch: MathF.Sin(time * DRILL_VIBRATION_FREQUENCY_PITCH) * DRILL_VIBRATION_PITCH_RADIANS,
            roll: MathF.Sin(time * DRILL_VIBRATION_FREQUENCY_ROLL) * DRILL_VIBRATION_ROLL_RADIANS
        );
        return new ChildComponent(resting.Parent)
        {
            LocalPosition = resting.LocalPosition + positionOffset,
            LocalOrientation = Quaternion.Normalize(resting.LocalOrientation * rotationOffset),
            LocalScale = resting.LocalScale,
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
        _vibrationTime = 0d;
        _isVibrating = false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _viewModelEntity = null;
        _currentKey = null;
        _restingChild = default;
        _vibrationTime = 0d;
        _isVibrating = false;
    }
}
