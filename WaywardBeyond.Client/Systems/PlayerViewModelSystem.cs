using System;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Items;
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
    in InteractionState interactionState
) : IEntitySystem, IDisposable {
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

    private Entity? _viewModelEntity;
    private ViewModelKey? _currentKey;

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

            if (key.Equals(Owner._currentKey))
            {
                return;
            }

            Owner._currentKey = key;
            Owner.ClearRenderer();

            ResolvedVisuals visuals = Owner.ResolveVisuals(item, viewModel, shape);

            var meshRenderer = new MeshRenderer(visuals.Mesh, visuals.Material, visuals.RenderOptions);
            Owner._viewModelEntity.Value.AddOrUpdate(new MeshRendererComponent(meshRenderer));
            Owner._viewModelEntity.Value.AddOrUpdate(new ChildComponent(store.GetUuid(entity))
            {
                LocalPosition = new Vector3(viewModel.Position.X, viewModel.Position.Y, viewModel.Position.Z),
                LocalOrientation = Quaternion.CreateFromYawPitchRoll(
                    viewModel.Rotation.Y * MathS.DEGREES_TO_RADIANS,
                    viewModel.Rotation.X * MathS.DEGREES_TO_RADIANS,
                    viewModel.Rotation.Z * MathS.DEGREES_TO_RADIANS
                ),
                LocalScale = new Vector3(viewModel.Scale.X, viewModel.Scale.Y, viewModel.Scale.Z),
            });
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
                Result<Mesh> brickMesh = _brickMeshBuilder.Build(brick, shape, Orientation.Identity);
                if (brickMesh.Success)
                {
                    RenderOptions options = _voxelMaterials.GetRenderOptions(brick.Alpha);
                    options.CastShadows = false;
                    options.ReceiveShadows = true;
                    return new ResolvedVisuals(brickMesh.Value, _voxelMaterials.GetMaterial(brick.Alpha), options);
                }
            }
        }

        Mesh fallbackMesh = _brickMeshBuilder.BuildFallback();
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

    private void FreeViewModel(DataStore store)
    {
        if (_viewModelEntity.HasValue)
        {
            store.Free(_viewModelEntity.Value.Ptr);
        }

        _viewModelEntity = null;
        _currentKey = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _viewModelEntity = null;
        _currentKey = null;
    }
}
