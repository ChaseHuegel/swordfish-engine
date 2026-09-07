using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Physics;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// The shared, deterministic player-motion step. There is one instance per world - never a shared
/// singleton - because each world keeps its own sim-tick counter and input staging. It subscribes to the
/// world's <see cref="IPhysics.FixedUpdate"/> so it runs exactly once per fixed physics step (0.016s),
/// applying the tick-tagged input command for the current sim tick. Look sets the entity orientation
/// from the absolute yaw/pitch (clamped per step); movement derives world-space velocity from the
/// command's Movement vector and the current orientation. It never writes Torque - the player look is
/// kinematic and rides the entity-wins sync cycle.
/// </summary>
public sealed class SharedPlayerMotionStep : IDisposable
{
    /// <summary>Resolves the input command to apply for a given sim tick on a given entity.</summary>
    public delegate bool CommandResolver(int entity, uint simTick, out InputComponent command);

    private readonly DataStore _store;
    private readonly IPhysics _physics;
    private readonly CommandResolver _resolveCommand;

    private readonly Dictionary<int, bool> _jumpStates = [];

    public uint CurrentSimTick { get; private set; }

    /// <summary>Re-seeds the sim-tick counter, used by reconcile to align prediction with the server.</summary>
    public void AlignTo(uint simTick)
    {
        CurrentSimTick = simTick;
    }

    public SharedPlayerMotionStep(
        DataStore store,
        in IPhysics physics,
        CommandResolver resolveCommand
    ) {
        _store = store;
        _physics = physics;
        _resolveCommand = resolveCommand;
        physics.FixedUpdate += OnFixedUpdate;
    }

    public void Dispose()
    {
        _physics.FixedUpdate -= OnFixedUpdate;
    }

    private void OnFixedUpdate(object? sender, EventArgs e)
    {
        Step();
    }

    public void Step()
    {
        CurrentSimTick++;

        _store.QueryRef<InputComponent, PhysicsComponent, TransformComponent>(0f,
            (float delta, DataStore store, int entity, ref Ref<InputComponent> input, ref Ref<PhysicsComponent> physics, ref Ref<TransformComponent> transform) =>
            {
                Execute(entity, ref physics, ref transform);
            });
    }

    private void Execute(int entity, ref Ref<PhysicsComponent> physics, ref Ref<TransformComponent> transform)
    {
        //  The command for the current sim tick. Newest-for-tick collapse is handled by the resolver;
        //  a missing command simply leaves the pose unchanged this step (idle).
        if (!_resolveCommand(entity, CurrentSimTick, out InputComponent command))
        {
            return;
        }

        ref PhysicsComponent physicsValue = ref physics.Write;
        ref readonly TransformComponent transformValue = ref transform.Read;

        //  Look: drive rotation through physics torque (restored physics-look feel). The resolved
        //  per-axis deltas are world-space; transform them into the body frame by the current orientation
        //  and let Jolt integrate. Both prediction and the authoritative server apply the identical
        //  command at the same sim tick, and their solves are serialized, so the rotation is deterministic.
        physicsValue.Torque += -physicsValue.Torque * PlayerBodyConfig.PHYSICS_STEP * PlayerBodyConfig.ANGULAR_DECELERATION;
        if (physicsValue.Torque.LengthSquared() <= 0.00001f)
        {
            physicsValue.Torque = new Vector3();
        }

        var lookDelta = new Vector3(command.LookPitchDelta, command.LookYawDelta, command.LookRollDelta);
        physicsValue.Torque += Vector3.Transform(lookDelta, transform.Read.Orientation);

        //  Movement: derive world-space direction from the command and the current orientation.
        Vector3 forward = transformValue.GetForward();
        Vector3 right = transformValue.GetRight();
        Vector3 up = transformValue.GetUp();
        var direction = command.MovementX * right + command.MovementY * up + command.MovementZ * forward;

        //  Damping then input-driven velocity (matches the legacy floaty-motion feel).
        physicsValue.Velocity += -physicsValue.Velocity * PlayerBodyConfig.PHYSICS_STEP * PlayerBodyConfig.DECELERATION;
        if (physicsValue.Velocity.LengthSquared() <= 0.00001f)
        {
            physicsValue.Velocity = new Vector3();
        }

        physicsValue.Velocity += direction * PlayerBodyConfig.BASE_SPEED * PlayerBodyConfig.PHYSICS_STEP;

        //  Jump is one-shot on the rising edge of the Jump flag.
        bool wasDown = _jumpStates.TryGetValue(entity, out bool previous) && previous;
        if (command.Jump && !wasDown)
        {
            physicsValue.Velocity += new Vector3(0f, PlayerBodyConfig.JUMP_SPEED, 0f);
        }

        _jumpStates[entity] = command.Jump;
    }
}