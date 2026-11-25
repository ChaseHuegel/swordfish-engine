using System;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Client.Voxels.Models;

namespace WaywardBeyond.Client.Systems;

/// <summary>Drives the main menu camera drift and manages the floating background spaceship entity.</summary>
internal sealed class MainMenuAnimationSystem(
    in ILogger<MainMenuAnimationSystem> logger,
    in IAssetDatabase<VoxelEntityModel> blueprintDatabase,
    in VoxelEntityBuilder voxelEntityBuilder
) : IEntitySystem
{
    internal const string MENU_BLUEPRINT_ID = "builtin/mainMenu";
    internal const string MENU_ENTITY_TAG = "menu";
    internal const float BOB_STRENGTH = 0.1f;

    internal static readonly Vector3 BaseOffset = new(5f, 0f, -20f);
    internal static readonly Quaternion BaseOrientation = Quaternion.CreateFromYawPitchRoll(yaw: -0.55f, pitch: 0.2f, roll: -0.1f);

    private readonly ILogger<MainMenuAnimationSystem> _logger = logger;
    private readonly IAssetDatabase<VoxelEntityModel> _blueprintDatabase = blueprintDatabase;
    private readonly VoxelEntityBuilder _voxelEntityBuilder = voxelEntityBuilder;
    private readonly Vector3 _axis = new(Random.Shared.NextSingle(), Random.Shared.NextSingle(), Random.Shared.NextSingle());

    private int _spaceshipEntity = -1;
    private Uuid _spaceshipUuid = Uuid.Null;
    private Vector3 _spaceshipScale = Vector3.One;
    private float _elapsed;
    private bool _wasInMainMenu;
    private bool _loadFailed;

    /// <summary>Computes the gentle bobbing offset at the given elapsed time in seconds.</summary>
    internal static Vector3 ComputeBobOffset(float elapsed)
    {
        float sin = MathF.Sin(elapsed);
        float cos = MathF.Cos(elapsed);
        return new Vector3(
            x: sin * BOB_STRENGTH * cos,
            y: sin * BOB_STRENGTH,
            z: 0f
        );
    }

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        if (WaywardBeyond.GameState != GameState.MainMenu)
        {
            if (_wasInMainMenu)
            {
                RemoveSpaceship(store);
                _wasInMainMenu = false;
            }

            return;
        }

        bool enteringMainMenu = !_wasInMainMenu;
        CameraAction cameraAction = new()
        {
            Owner = this,
            ResetCamera = enteringMainMenu,
        };
        store.QueryRef<TransformComponent, CameraComponent, CameraAction>(delta, ref cameraAction);

        if (!cameraAction.Found)
        {
            return;
        }

        if (enteringMainMenu)
        {
            _elapsed = 0f;
            _wasInMainMenu = true;
        }

        if (!HasActiveSpaceship(store) && !_loadFailed)
        {
            TrySpawnSpaceship(store, cameraAction.CameraUuid, in cameraAction.CameraTransform);
        }

        if (!HasActiveSpaceship(store))
        {
            return;
        }

        _elapsed += delta;
        Vector3 localPosition = BaseOffset + ComputeBobOffset(_elapsed);
        ApplySpaceshipPose(store, cameraAction.CameraUuid, in cameraAction.CameraTransform, in localPosition);
    }

    private bool HasActiveSpaceship(DataStore store)
    {
        return _spaceshipEntity >= 0
            && _spaceshipUuid != Uuid.Null
            && store.GetUuid(_spaceshipEntity) == _spaceshipUuid;
    }

    private void TrySpawnSpaceship(DataStore store, Uuid cameraUuid, in TransformComponent cameraTransform)
    {
        Result<VoxelEntityModel> blueprintResult = _blueprintDatabase.Get(MENU_BLUEPRINT_ID);
        if (!blueprintResult.Success || blueprintResult.Value.VoxelObject == null)
        {
            _loadFailed = true;
            _logger.LogError(
                blueprintResult.Exception,
                "Failed to load the background blueprint \"{BlueprintId}\": {Message}",
                MENU_BLUEPRINT_ID,
                blueprintResult.Message
            );
            return;
        }

        VoxelEntityModel model = blueprintResult.Value;
        Vector3 scale = model.Scale == Vector3.Zero ? Vector3.One : model.Scale;
        Vector3 worldPosition = cameraTransform.Position + Vector3.Transform(BaseOffset, cameraTransform.Orientation);
        Quaternion worldOrientation = cameraTransform.Orientation * BaseOrientation;

        Entity entity = _voxelEntityBuilder.Create(
            store,
            Uuid.NewUuid(),
            model.VoxelObject,
            worldPosition,
            worldOrientation,
            scale,
            tag: MENU_ENTITY_TAG,
            withPhysics: false
        );

        _spaceshipEntity = entity.Ptr;
        _spaceshipUuid = entity.Uuid;
        _spaceshipScale = scale;

        store.AddOrUpdate(_spaceshipEntity, new ChildComponent(cameraUuid)
        {
            LocalPosition = BaseOffset,
            LocalOrientation = BaseOrientation,
            LocalScale = scale,
        });
    }

    private void ApplySpaceshipPose(DataStore store, Uuid cameraUuid, in TransformComponent cameraTransform, in Vector3 localPosition)
    {
        store.AddOrUpdate(_spaceshipEntity, new ChildComponent(cameraUuid)
        {
            LocalPosition = localPosition,
            LocalOrientation = BaseOrientation,
            LocalScale = _spaceshipScale,
        });

        Vector3 worldPosition = cameraTransform.Position + Vector3.Transform(localPosition, cameraTransform.Orientation);
        Quaternion worldOrientation = cameraTransform.Orientation * BaseOrientation;
        store.AddOrUpdate(_spaceshipEntity, new TransformComponent(worldPosition, worldOrientation, _spaceshipScale));
    }

    private void RemoveSpaceship(DataStore store)
    {
        if (HasActiveSpaceship(store))
        {
            VoxelEntityBuilder.Destroy(store, _spaceshipEntity);
        }

        _spaceshipEntity = -1;
        _spaceshipUuid = Uuid.Null;
    }

    private struct CameraAction : IForEachRef<TransformComponent, CameraComponent>
    {
        public MainMenuAnimationSystem Owner;
        public bool ResetCamera;
        public bool Found;
        public Uuid CameraUuid;
        public TransformComponent CameraTransform;

        public void Execute(float delta, DataStore store, int entity, ref Ref<TransformComponent> transformComponent, ref Ref<CameraComponent> cameraComponent)
        {
            ref TransformComponent transform = ref transformComponent.Write;
            if (ResetCamera)
            {
                store.Remove<ChildComponent>(entity);
                transform.Position = Vector3.Zero;
                transform.Orientation = Quaternion.Identity;
            }

            transform.Rotate(Owner._axis * delta);

            if (!Found)
            {
                Found = true;
                CameraUuid = store.GetUuid(entity);
                CameraTransform = transform;
            }
        }
    }
}