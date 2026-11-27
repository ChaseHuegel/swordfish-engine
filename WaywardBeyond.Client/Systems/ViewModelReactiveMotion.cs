using System;
using System.Numerics;
using Swordfish.ECS;

namespace WaywardBeyond.Client.Systems;

/// <summary>Computes clamped, frame-rate-independent reactive 6-DOF motion offsets for the first-person view model.</summary>
internal struct ViewModelReactiveMotion
{
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

    private const float MAX_STEP_ANGULAR_DELTA = 0.25f;
    private const float REST_EPSILON_SQ = 1e-8f;

    private Vector3 _velocityPosOffset;
    private Vector3 _velocityRotOffset;
    private Vector3 _lookPosOffset;
    private Vector3 _lookRotOffset;
    private Quaternion _lastOrientation;
    private bool _hasLastOrientation;

    /// <summary>Current clamped player-local positional offset in meters.</summary>
    public Vector3 PositionOffset { get; private set; }

    /// <summary>Current clamped player-local Euler rotational offset (pitch, yaw, roll) in radians.</summary>
    public Vector3 RotationOffset { get; private set; }

    /// <summary>Clears all smoothed motion offsets and orientation history.</summary>
    public void Reset()
    {
        _velocityPosOffset = Vector3.Zero;
        _velocityRotOffset = Vector3.Zero;
        _lookPosOffset = Vector3.Zero;
        _lookRotOffset = Vector3.Zero;
        _lastOrientation = Quaternion.Identity;
        _hasLastOrientation = false;
        PositionOffset = Vector3.Zero;
        RotationOffset = Vector3.Zero;
    }

    /// <summary>Seeds the orientation baseline so the next tick produces no single-frame angular spike.</summary>
    public void SeedOrientation(in Quaternion orientation)
    {
        if (TryNormalizeOrientation(in orientation, out Quaternion normalized))
        {
            _lastOrientation = normalized;
            _hasLastOrientation = true;
        }
        else
        {
            _lastOrientation = Quaternion.Identity;
            _hasLastOrientation = false;
        }
    }

    /// <summary>Advances velocity inertia and angular sway over <paramref name="delta"/> seconds.</summary>
    public void Update(float delta, bool hasTransform, in TransformComponent transform, bool hasPhysics, in PhysicsComponent physics)
    {
        Quaternion currentOrientation = Quaternion.Identity;
        bool validOrientation = hasTransform && TryNormalizeOrientation(in transform.Orientation, out currentOrientation);
        if (!float.IsFinite(delta) || delta <= 0f)
        {
            if (!_hasLastOrientation && validOrientation)
            {
                _lastOrientation = currentOrientation;
                _hasLastOrientation = true;
            }

            return;
        }

        float decay = Math.Clamp(MathF.Exp(-SMOOTH_RATE * delta), 0f, 1f);
        float alpha = 1f - decay;

        Vector3 velocityPosTarget = Vector3.Zero;
        Vector3 velocityRotTarget = Vector3.Zero;

        //  Decay angular sway continuously every tick, then inject bounded displacement impulses
        //  when orientation changes so higher ECS tick rates do not alias against fixed physics steps.
        _lookPosOffset *= decay;
        _lookRotOffset *= decay;

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

            if (_hasLastOrientation)
            {
                Vector3 localAngularDelta = ComputeLocalAngularDelta(in _lastOrientation, in currentOrientation);
                if (localAngularDelta != Vector3.Zero)
                {
                    Vector3 clampedDelta = ClampVector(
                        localAngularDelta,
                        MAX_STEP_ANGULAR_DELTA,
                        MAX_STEP_ANGULAR_DELTA,
                        MAX_STEP_ANGULAR_DELTA
                    );
                    float impulseScale = SMOOTH_RATE * Math.Clamp(MathF.Exp(-0.5f * SMOOTH_RATE * delta), 0f, 1f);
                    Vector3 scaledDelta = clampedDelta * impulseScale;

                    _lookRotOffset = ClampVector(
                        _lookRotOffset + new Vector3(
                            -scaledDelta.X * LOOK_PITCH_SCALE,
                            -scaledDelta.Y * LOOK_YAW_SCALE,
                            -scaledDelta.Z * LOOK_ROLL_SCALE
                        ),
                        MAX_LOOK_PITCH,
                        MAX_LOOK_YAW,
                        MAX_LOOK_ROLL
                    );
                    _lookPosOffset = ClampVector(
                        _lookPosOffset + new Vector3(
                            scaledDelta.Y * LOOK_POS_YAW_SCALE,
                            -scaledDelta.X * LOOK_POS_PITCH_SCALE,
                            0f
                        ),
                        MAX_LOOK_POS_X,
                        MAX_LOOK_POS_Y,
                        0f
                    );
                }
            }

            _lastOrientation = currentOrientation;
            _hasLastOrientation = true;
        }
        else
        {
            _hasLastOrientation = false;
        }

        _velocityPosOffset = Vector3.Lerp(_velocityPosOffset, velocityPosTarget, alpha);
        _velocityRotOffset = Vector3.Lerp(_velocityRotOffset, velocityRotTarget, alpha);

        if (velocityPosTarget.LengthSquared() <= REST_EPSILON_SQ && _velocityPosOffset.LengthSquared() <= REST_EPSILON_SQ)
        {
            _velocityPosOffset = Vector3.Zero;
        }

        if (velocityRotTarget.LengthSquared() <= REST_EPSILON_SQ && _velocityRotOffset.LengthSquared() <= REST_EPSILON_SQ)
        {
            _velocityRotOffset = Vector3.Zero;
        }

        if (_lookPosOffset.LengthSquared() <= REST_EPSILON_SQ)
        {
            _lookPosOffset = Vector3.Zero;
        }

        if (_lookRotOffset.LengthSquared() <= REST_EPSILON_SQ)
        {
            _lookRotOffset = Vector3.Zero;
        }

        PositionOffset = ClampVector(
            _velocityPosOffset + _lookPosOffset,
            MAX_POSITION_OFFSET_X,
            MAX_POSITION_OFFSET_Y,
            MAX_POSITION_OFFSET_Z
        );
        RotationOffset = ClampVector(
            _velocityRotOffset + _lookRotOffset,
            MAX_ROTATION_OFFSET_PITCH,
            MAX_ROTATION_OFFSET_YAW,
            MAX_ROTATION_OFFSET_ROLL
        );
    }

    /// <summary>Composes the current reactive motion offsets onto <paramref name="resting"/> in player-local space.</summary>
    public readonly ChildComponent ApplyTo(in ChildComponent resting)
    {
        if (PositionOffset == Vector3.Zero && RotationOffset == Vector3.Zero)
        {
            return resting;
        }

        Quaternion localOrientation = RotationOffset == Vector3.Zero
            ? resting.LocalOrientation
            : Quaternion.Normalize(
                Quaternion.CreateFromYawPitchRoll(RotationOffset.Y, RotationOffset.X, RotationOffset.Z) * resting.LocalOrientation
            );

        return new ChildComponent(resting.Parent)
        {
            LocalPosition = resting.LocalPosition + PositionOffset,
            LocalOrientation = localOrientation,
            LocalScale = resting.LocalScale,
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
}
