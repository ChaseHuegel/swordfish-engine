using System;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Networking;

namespace WaywardBeyond.Client.Player;

/// <summary>
/// Reconciles the local player's authoritative transform and physics without snapping its
/// client-predicted look. Position corrections are deadbanded and blended; collision rotation becomes
/// authoritative through a smoothed target; the server's angular velocity is applied only while the
/// look is idle.
/// </summary>
internal sealed class LocalPlayerReconciler(ILogger<LocalPlayerReconciler> logger)
{
    //  Collision rotation reconciles over roughly 100 ms.
    private const float CORRECTION_RATE = 30f;
    private const float CONVERGED_DOT = 0.999999f;
    private const float LOOK_IDLE_EPSILON_SQ = 1e-10f;
    private const float MIN_ANGULAR_SPEED = 1e-6f;
    private const float MIN_ROTATION = 1e-6f;
    private const float MIN_QUATERNION_LENGTH_SQ = 1e-12f;
    private const float POSITION_CORRECTION_BLEND = 0.5f;
    private const float POSITION_DEADBAND_SQ = 0.0001f;

    private Uuid _player = Uuid.Null;
    private Quaternion _targetOrientation = Quaternion.Identity;
    private Vector3 _targetAngularVelocity;
    private Vector3 _ackedLook;
    private bool _hasAuthoritativeTransform;

    /// <summary>Drops the target state for the next session.</summary>
    public void Reset()
    {
        _player = Uuid.Null;
        _targetOrientation = Quaternion.Identity;
        _targetAngularVelocity = Vector3.Zero;
        _ackedLook = Vector3.Zero;
        _hasAuthoritativeTransform = false;
    }

    /// <summary>Applies an authoritative transform, records its orientation target, and blends position corrections.</summary>
    public void ApplyTransform(DataStore store, int entity, Vector3 position, Quaternion orientation, Vector3 scale)
    {
        if (!EnsurePlayer(store, entity))
        {
            return;
        }

        if (!IsFinite(position) || !IsFinite(scale) || !IsFinite(orientation)
            || orientation.LengthSquared() < MIN_QUATERNION_LENGTH_SQ)
        {
            logger.LogWarning("Ignoring a non-finite local player transform snapshot.");
            return;
        }

        float blend = _hasAuthoritativeTransform ? POSITION_CORRECTION_BLEND : 1f;
        var write = new SeatTransform(in position, in scale, blend, POSITION_DEADBAND_SQ);
        store.QueryRef<TransformComponent, SeatTransform>(entity, 0f, ref write);
        _hasAuthoritativeTransform = true;

        _targetOrientation = Quaternion.Normalize(orientation);
    }

    /// <summary>Applies authoritative local-player linear velocity and records the angular velocity.</summary>
    public void ApplyPhysics(DataStore store, int entity, Vector3 velocity, Vector3 angularVelocity)
    {
        if (!EnsurePlayer(store, entity))
        {
            return;
        }

        if (!IsFinite(velocity) || !IsFinite(angularVelocity))
        {
            logger.LogWarning("Ignoring a non-finite local player physics snapshot.");
            return;
        }

        var write = new SeatVelocity(in velocity);
        store.QueryRef<PhysicsComponent, SeatVelocity>(entity, 0f, ref write);

        _targetAngularVelocity = angularVelocity;
    }

    /// <summary>Updates the acked look and applies the server angular velocity while look is idle.</summary>
    public void ApplySnapshot(DataStore store, uint ackTick)
    {
        if (_player == Uuid.Null || !store.TryGet(_player, out int entity))
        {
            return;
        }

        _ackedLook = FindAckedLook(store, entity, ackTick, _ackedLook);

        if (!store.TryGet(entity, out InputComponent input))
        {
            return;
        }

        var currentLook = new Vector3(input.LookPitch, input.LookYaw, input.LookRoll);
        if (!IsLookIdle(currentLook, _ackedLook))
        {
            return;
        }

        var write = new SeatAngularVelocity(in _targetAngularVelocity);
        store.QueryRef<PhysicsComponent, SeatAngularVelocity>(entity, 0f, ref write);
    }

    /// <summary>Advances the target orientation and slerps the local player toward it.</summary>
    public void Tick(float delta, DataStore store)
    {
        if (_player == Uuid.Null
            || !store.TryGet(_player, out int entity)
            || !store.TryGet(entity, out InputComponent input))
        {
            return;
        }

        var currentLook = new Vector3(input.LookPitch, input.LookYaw, input.LookRoll);
        _targetAngularVelocity *= MathF.Exp(-CharacterDefaults.ANGULAR_DECELERATION * delta);
        if (IsLookIdle(currentLook, _ackedLook))
        {
            AdvanceTargetOrientation(delta);
        }

        Quaternion target = ComputeOrientationTarget(_targetOrientation, currentLook, _ackedLook);
        float blend = 1f - MathF.Exp(-CORRECTION_RATE * delta);
        var action = new SlerpOrientation(in target, blend);
        store.QueryRef<TransformComponent, SlerpOrientation>(entity, 0f, ref action);
    }

    private bool EnsurePlayer(DataStore store, int entity)
    {
        Uuid player = store.GetUuid(entity);
        if (player == Uuid.Null)
        {
            return false;
        }

        if (player != _player)
        {
            Reset();
            _player = player;
        }

        return true;
    }

    /// <summary>Returns the look totals at the ack, holding the fallback once the ring is trimmed.</summary>
    private static Vector3 FindAckedLook(DataStore store, int entity, uint ackTick, Vector3 fallback)
    {
        if (!store.TryGet(entity, out PendingInputComponent pending) || pending.Ring == null)
        {
            return fallback;
        }

        //  The ring is trimmed at or below each ack, so an empty lookup means the ack has not advanced
        //  past the totals already known. Keep them rather than falling back to zero.
        Result<InputComponent> acked = pending.Ring.GetCommand(ackTick);
        return acked.Success
            ? new Vector3(acked.Value.LookPitch, acked.Value.LookYaw, acked.Value.LookRoll)
            : fallback;
    }

    /// <summary>Whether the live look totals match the ack, so the server's angular velocity is safe to apply.</summary>
    private static bool IsLookIdle(Vector3 currentLook, Vector3 ackedLook)
    {
        return Vector3.DistanceSquared(currentLook, ackedLook) <= LOOK_IDLE_EPSILON_SQ;
    }

    /// <summary>Advances the target orientation by the server angular velocity.</summary>
    private void AdvanceTargetOrientation(float delta)
    {
        float speed = _targetAngularVelocity.Length();
        if (speed <= MIN_ANGULAR_SPEED)
        {
            return;
        }

        Vector3 axis = _targetAngularVelocity / speed;
        Quaternion step = Quaternion.CreateFromAxisAngle(axis, speed * delta);
        _targetOrientation = Quaternion.Normalize(step * _targetOrientation);
    }

    /// <summary>Builds the target from the server orientation and the look applied after its ack.</summary>
    private static Quaternion ComputeOrientationTarget(Quaternion orientation, Vector3 currentLook, Vector3 ackedLook)
    {
        Vector3 delta = currentLook - ackedLook;
        float angle = delta.Length();
        Quaternion pendingLook = angle > MIN_ROTATION
            ? Quaternion.CreateFromAxisAngle(delta / angle, angle)
            : Quaternion.Identity;

        return Quaternion.Normalize(orientation * pendingLook);
    }

    private static bool IsFinite(in Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    private static bool IsFinite(in Quaternion value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y)
            && float.IsFinite(value.Z) && float.IsFinite(value.W);
    }

    private readonly struct SeatTransform(in Vector3 position, in Vector3 scale, float blend, float deadbandSq) : IForEachRef<TransformComponent>
    {
        private readonly Vector3 _position = position;
        private readonly Vector3 _scale = scale;
        private readonly float _blend = blend;
        private readonly float _deadbandSq = deadbandSq;

        /// <inheritdoc/>
        public void Execute(float delta, DataStore store, int entity, ref Ref<TransformComponent> transform)
        {
            ref readonly TransformComponent current = ref transform.Read;
            Vector3 offset = _position - current.Position;
            bool positionChanged = offset.LengthSquared() > _deadbandSq;
            bool scaleChanged = current.Scale != _scale;
            if (!positionChanged && !scaleChanged)
            {
                return;
            }

            ref TransformComponent value = ref transform.Write;
            if (positionChanged)
            {
                value.Position = _blend >= 1f ? _position : current.Position + offset * _blend;
            }

            if (scaleChanged)
            {
                value.Scale = _scale;
            }
        }
    }

    private readonly struct SeatVelocity(in Vector3 velocity) : IForEachRef<PhysicsComponent>
    {
        private readonly Vector3 _velocity = velocity;

        /// <inheritdoc/>
        public void Execute(float delta, DataStore store, int entity, ref Ref<PhysicsComponent> physics)
        {
            if (physics.Read.Velocity == _velocity)
            {
                return;
            }

            physics.Write.Velocity = _velocity;
        }
    }

    private readonly struct SeatAngularVelocity(in Vector3 angularVelocity) : IForEachRef<PhysicsComponent>
    {
        private readonly Vector3 _angularVelocity = angularVelocity;

        /// <inheritdoc/>
        public void Execute(float delta, DataStore store, int entity, ref Ref<PhysicsComponent> physics)
        {
            if (physics.Read.Torque == _angularVelocity)
            {
                return;
            }

            physics.Write.Torque = _angularVelocity;
        }
    }

    private readonly struct SlerpOrientation(in Quaternion target, float blend) : IForEachRef<TransformComponent>
    {
        private readonly Quaternion _target = target;

        /// <inheritdoc/>
        public void Execute(float delta, DataStore store, int entity, ref Ref<TransformComponent> transform)
        {
            Quaternion current = transform.Read.Orientation;
            Quaternion target = _target;
            float dot = Quaternion.Dot(current, target);
            if (dot < 0f)
            {
                target = new Quaternion(-target.X, -target.Y, -target.Z, -target.W);
                dot = -dot;
            }

            if (dot > CONVERGED_DOT)
            {
                return;
            }

            transform.Write.Orientation = Quaternion.Normalize(Quaternion.Slerp(current, target, blend));
        }
    }
}
