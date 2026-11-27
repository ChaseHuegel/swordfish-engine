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
    /// <summary>Maximum reactive local X positional offset in meters.</summary>
    internal const float MAX_POSITION_OFFSET_X = 0.045f;
    /// <summary>Maximum reactive local Y positional offset in meters.</summary>
    internal const float MAX_POSITION_OFFSET_Y = 0.040f;
    /// <summary>Maximum reactive local Z positional offset in meters.</summary>
    internal const float MAX_POSITION_OFFSET_Z = 0.030f;
    /// <summary>Maximum reactive local pitch offset in radians.</summary>
    internal const float MAX_ROTATION_OFFSET_PITCH = 0.08f;
    /// <summary>Maximum reactive local yaw offset in radians.</summary>
    internal const float MAX_ROTATION_OFFSET_YAW = 0.08f;
    /// <summary>Maximum reactive local roll offset in radians.</summary>
    internal const float MAX_ROTATION_OFFSET_ROLL = 0.09f;

    private const float SMOOTH_RATE = 10f;
    private const float VELOCITY_POS_SCALE_X = 0.006f;
    private const float VELOCITY_POS_SCALE_Y = 0.005f;
    private const float VELOCITY_POS_SCALE_Z = 0.005f;
    private const float MAX_VELOCITY_POS_X = 0.035f;
    private const float MAX_VELOCITY_POS_Y = 0.030f;
    private const float MAX_VELOCITY_POS_Z = 0.030f;
    private const float VELOCITY_PITCH_SCALE = 0.006f;
    private const float VELOCITY_ROLL_SCALE = 0.007f;
    private const float MAX_VELOCITY_PITCH = 0.045f;
    private const float MAX_VELOCITY_ROLL = 0.055f;

    private const float LOOK_PITCH_SCALE = 0.018f;
    private const float LOOK_YAW_SCALE = 0.020f;
    private const float LOOK_ROLL_SCALE = 0.025f;
    private const float MAX_LOOK_PITCH = 0.06f;
    private const float MAX_LOOK_YAW = 0.07f;
    private const float MAX_LOOK_ROLL = 0.07f;
    private const float LOOK_POS_YAW_SCALE = 0.008f;
    private const float LOOK_POS_PITCH_SCALE = 0.007f;
    private const float MAX_LOOK_POS_X = 0.025f;
    private const float MAX_LOOK_POS_Y = 0.020f;

    private const float MIN_ANGULAR_DELTA = 1e-4f;
    private const float REST_EPSILON_SQ = 1e-8f;

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
    private ReactiveMotionState _motion;

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

        if (!WaywardBeyond.IsPlaying())
        {
            _motion.Reset();
        }

        ForEachAction action = new() { Owner = this };
        store.Query<PlayerComponent, InventoryComponent, ForEachAction>(delta, ref action);

        if (!action.VisitedPlayer)
        {
            _motion.Reset();
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

        ChildComponent pose = ApplyReactiveMotion(in _restingChild);

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

    private ChildComponent ApplyReactiveMotion(in ChildComponent resting)
    {
        if (_motion.PositionOffset == Vector3.Zero && _motion.RotationOffset == Vector3.Zero)
        {
            return resting;
        }

        Quaternion swayOrientation = _motion.CreateRotationQuaternion();
        Quaternion localOrientation = swayOrientation == Quaternion.Identity
            ? resting.LocalOrientation
            : Quaternion.Normalize(swayOrientation * resting.LocalOrientation);

        return new ChildComponent(resting.Parent)
        {
            LocalPosition = resting.LocalPosition + _motion.PositionOffset,
            LocalOrientation = localOrientation,
            LocalScale = resting.LocalScale,
        };
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
        bool hadOffset = _isAnimating || _motion.PositionOffset != Vector3.Zero || _motion.RotationOffset != Vector3.Zero;
        _animationTime = 0d;
        _isAnimating = false;
        _motion.Reset();

        if (hadOffset && _viewModelEntity.HasValue && _restingChild.Parent != Uuid.Null)
        {
            WriteChildIfChanged(store, in _restingChild);
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

    private static bool TryNormalizeOrientation(in Quaternion orientation, out Quaternion normalized)
    {
        if (!float.IsFinite(orientation.X)
            || !float.IsFinite(orientation.Y)
            || !float.IsFinite(orientation.Z)
            || !float.IsFinite(orientation.W))
        {
            normalized = Quaternion.Identity;
            return false;
        }

        float lenSq = orientation.LengthSquared();
        if (!float.IsFinite(lenSq) || lenSq <= 1e-6f)
        {
            normalized = Quaternion.Identity;
            return false;
        }

        normalized = Quaternion.Normalize(orientation);
        return true;
    }

    private static bool IsFinite(in Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    private static Vector3 ComputeLocalAngularDelta(in Quaternion previous, in Quaternion current)
    {
        if (previous == current)
        {
            return Vector3.Zero;
        }

        Quaternion delta = Quaternion.Conjugate(previous) * current;
        if (delta.W < 0f)
        {
            delta = -delta;
        }

        var imag = new Vector3(delta.X, delta.Y, delta.Z);
        float sinHalf = imag.Length();
        if (sinHalf > 1e-6f)
        {
            float angle = 2f * MathF.Atan2(sinHalf, delta.W);
            return imag * (angle / sinHalf);
        }

        return imag * 2f;
    }

    private static Vector3 ClampVector(Vector3 value, float maxX, float maxY, float maxZ)
    {
        return new Vector3(
            Math.Clamp(value.X, -maxX, maxX),
            Math.Clamp(value.Y, -maxY, maxY),
            Math.Clamp(value.Z, -maxZ, maxZ)
        );
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

    private struct ReactiveMotionState
    {
        public Vector3 PositionOffset;
        public Vector3 RotationOffset;
        public Quaternion LastOrientation;
        public bool HasLastOrientation;

        public void Reset()
        {
            PositionOffset = Vector3.Zero;
            RotationOffset = Vector3.Zero;
            LastOrientation = Quaternion.Identity;
            HasLastOrientation = false;
        }

        public void SeedOrientation(in Quaternion orientation)
        {
            if (TryNormalizeOrientation(in orientation, out Quaternion normalized))
            {
                LastOrientation = normalized;
                HasLastOrientation = true;
            }
            else
            {
                LastOrientation = Quaternion.Identity;
                HasLastOrientation = false;
            }
        }

        public void Update(float delta, bool hasTransform, in TransformComponent transform, bool hasPhysics, in PhysicsComponent physics)
        {
            Quaternion currentOrientation = Quaternion.Identity;
            bool validOrientation = hasTransform && TryNormalizeOrientation(in transform.Orientation, out currentOrientation);
            if (!float.IsFinite(delta) || delta <= 0f)
            {
                if (!HasLastOrientation && validOrientation)
                {
                    LastOrientation = currentOrientation;
                    HasLastOrientation = true;
                }

                return;
            }

            Vector3 velocityPosTarget = Vector3.Zero;
            Vector3 velocityRotTarget = Vector3.Zero;
            Vector3 lookPosTarget = Vector3.Zero;
            Vector3 lookRotTarget = Vector3.Zero;

            if (validOrientation)
            {
                if (hasPhysics && IsFinite(in physics.Velocity))
                {
                    Vector3 localVelocity = Vector3.Transform(physics.Velocity, Quaternion.Conjugate(currentOrientation));
                    velocityPosTarget = new Vector3(
                        Math.Clamp(-localVelocity.X * VELOCITY_POS_SCALE_X, -MAX_VELOCITY_POS_X, MAX_VELOCITY_POS_X),
                        Math.Clamp(-localVelocity.Y * VELOCITY_POS_SCALE_Y, -MAX_VELOCITY_POS_Y, MAX_VELOCITY_POS_Y),
                        Math.Clamp(-localVelocity.Z * VELOCITY_POS_SCALE_Z, -MAX_VELOCITY_POS_Z, MAX_VELOCITY_POS_Z)
                    );
                    velocityRotTarget = new Vector3(
                        Math.Clamp(-localVelocity.Y * VELOCITY_PITCH_SCALE, -MAX_VELOCITY_PITCH, MAX_VELOCITY_PITCH),
                        0f,
                        Math.Clamp(-localVelocity.X * VELOCITY_ROLL_SCALE, -MAX_VELOCITY_ROLL, MAX_VELOCITY_ROLL)
                    );
                }

                if (HasLastOrientation)
                {
                    Vector3 localAngularDelta = ComputeLocalAngularDelta(in LastOrientation, in currentOrientation);
                    float invDelta = 1f / MathF.Max(delta, MIN_ANGULAR_DELTA);
                    Vector3 angularVelocity = localAngularDelta * invDelta;

                    lookRotTarget = new Vector3(
                        Math.Clamp(-angularVelocity.X * LOOK_PITCH_SCALE, -MAX_LOOK_PITCH, MAX_LOOK_PITCH),
                        Math.Clamp(-angularVelocity.Y * LOOK_YAW_SCALE, -MAX_LOOK_YAW, MAX_LOOK_YAW),
                        Math.Clamp(-angularVelocity.Z * LOOK_ROLL_SCALE, -MAX_LOOK_ROLL, MAX_LOOK_ROLL)
                    );
                    lookPosTarget = new Vector3(
                        Math.Clamp(angularVelocity.Y * LOOK_POS_YAW_SCALE, -MAX_LOOK_POS_X, MAX_LOOK_POS_X),
                        Math.Clamp(-angularVelocity.X * LOOK_POS_PITCH_SCALE, -MAX_LOOK_POS_Y, MAX_LOOK_POS_Y),
                        0f
                    );
                }

                LastOrientation = currentOrientation;
                HasLastOrientation = true;
            }
            else
            {
                HasLastOrientation = false;
            }

            Vector3 targetPos = ClampVector(
                velocityPosTarget + lookPosTarget,
                MAX_POSITION_OFFSET_X,
                MAX_POSITION_OFFSET_Y,
                MAX_POSITION_OFFSET_Z
            );
            Vector3 targetRot = ClampVector(
                velocityRotTarget + lookRotTarget,
                MAX_ROTATION_OFFSET_PITCH,
                MAX_ROTATION_OFFSET_YAW,
                MAX_ROTATION_OFFSET_ROLL
            );

            float alpha = Math.Clamp(1f - MathF.Exp(-SMOOTH_RATE * delta), 0f, 1f);
            PositionOffset = Vector3.Lerp(PositionOffset, targetPos, alpha);
            RotationOffset = Vector3.Lerp(RotationOffset, targetRot, alpha);

            if (targetPos.LengthSquared() <= REST_EPSILON_SQ && PositionOffset.LengthSquared() <= REST_EPSILON_SQ)
            {
                PositionOffset = Vector3.Zero;
            }

            if (targetRot.LengthSquared() <= REST_EPSILON_SQ && RotationOffset.LengthSquared() <= REST_EPSILON_SQ)
            {
                RotationOffset = Vector3.Zero;
            }
        }

        public readonly Quaternion CreateRotationQuaternion()
        {
            if (RotationOffset == Vector3.Zero)
            {
                return Quaternion.Identity;
            }

            return Quaternion.CreateFromYawPitchRoll(RotationOffset.Y, RotationOffset.X, RotationOffset.Z);
        }
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

            if (!key.Equals(Owner._currentKey))
            {
                Owner._currentKey = key;
                Owner._restingChild = CreateRestingChild(in playerUuid, in viewModel);
                Owner._isAnimating = false;
                Owner._animationTime = 0d;
                Owner._motion.Reset();
                if (hasTransform)
                {
                    Owner._motion.SeedOrientation(in playerTransform.Orientation);
                }

                Owner.ClearRenderer();

                ResolvedVisuals visuals = Owner.ResolveVisuals(item, viewModel, shape);

                var meshRenderer = new MeshRenderer(visuals.Mesh, visuals.Material, visuals.RenderOptions);
                Owner._viewModelEntity!.Value.AddOrUpdate(new MeshRendererComponent(meshRenderer));
                Owner._viewModelEntity.Value.AddOrUpdate(Owner._restingChild);
            }
            else
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
