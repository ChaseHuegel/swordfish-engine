using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Physics;
using WaywardBeyond.Networking.Components;

namespace WaywardBeyond.Gameplay;

/// <summary>
/// The shared, deterministic simulation step for a world. There is one instance per world - never a
/// shared singleton - because each world keeps its own sim-tick counter and input staging. It subscribes
/// to the world's <see cref="IPhysics.FixedUpdate"/> so it runs exactly once per fixed physics step
/// (0.016s), applying the tick-tagged input command for the current sim tick. It drives player motion
/// (look via torque, movement from the command's vector). Both the server authority and the client
/// prediction run the same step for determinism.
/// </summary>
public sealed class SimulationStep : IDisposable
{
    /// <summary>The fixed physics step (seconds) the shared motion step runs at, matching <c>JoltPhysicsSystem</c>.</summary>
    private const float PHYSICS_STEP = 0.016f;
    
    /// <summary>Resolves the input command to apply for a given sim tick on a given entity.</summary>
    public delegate bool CommandResolver(int entity, uint simTick, out InputComponent command);

    private readonly DataStore _store;
    private readonly IPhysics _physics;
    private readonly CommandResolver _resolveCommand;

    private readonly Dictionary<int, Vector3> _lastAppliedLook = [];

    public uint CurrentSimTick { get; private set; }

    /// <summary>Re-seeds the sim-tick counter, used by reconcile to align prediction with the server.</summary>
    public void AlignTo(uint simTick)
    {
        CurrentSimTick = simTick;
    }

    public SimulationStep(
        DataStore store,
        in IPhysics physics,
        CommandResolver? resolveCommand = null
    ) {
        _store = store;
        _physics = physics;
        _resolveCommand = resolveCommand ?? ((int entity, uint simTick, out InputComponent command) => ResolveStagedInput(_store, entity, simTick, out command));
        physics.FixedUpdate += OnFixedUpdate;
    }

    /// <summary>Resolves the staged input for a sim tick from the store's <see cref="NetworkComponent"/> buffer.</summary>
    private static bool ResolveStagedInput(DataStore store, int entity, uint simTick, out InputComponent command)
    {
        if (store.TryGet(entity, out NetworkComponent net) && net.StagedInputs != null)
        {
            return net.StagedInputs.TryGet(simTick, out command);
        }

        command = default;
        return false;
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
        PruneLookHistory();

        _store.QueryRef<InputComponent, PhysicsComponent, TransformComponent>(0f,
            (float _, DataStore _, int entity, ref Ref<InputComponent> _, ref Ref<PhysicsComponent> physics, ref Ref<TransformComponent> transform) =>
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

        //  Look: drive rotation through physics torque (restored physics-look feel). The wire carries the
        //  running absolute look totals; take the per-sim-tick difference so no input is dropped regardless
        //  of sampling cadence, then apply it decoupled from dt (delta / PHYSICS_STEP) so each step rotates
        //  by the full accumulated delta and Q/E roll is visible. World-space delta -> body frame by the
        //  current orientation; Jolt integrates. Both sides consume the same totals, serialized solves make
        //  the rotation deterministic.
        physicsValue.Torque += -physicsValue.Torque * PHYSICS_STEP * CharacterDefaults.ANGULAR_DECELERATION;
        if (physicsValue.Torque.LengthSquared() <= 0.00001f)
        {
            physicsValue.Torque = new Vector3();
        }

        var lookTotals = new Vector3(command.LookPitch, command.LookYaw, command.LookRoll);
        if (_lastAppliedLook.TryGetValue(entity, out Vector3 lastApplied))
        {
            Vector3 perStep = lookTotals - lastApplied;
            if (perStep != Vector3.Zero)
            {
                physicsValue.Torque += Vector3.Transform(perStep / PHYSICS_STEP, transform.Read.Orientation);
            }
        }
        _lastAppliedLook[entity] = lookTotals;

        //  Movement: derive world-space direction from the command and the current orientation.
        Vector3 forward = transformValue.GetForward();
        Vector3 right = transformValue.GetRight();
        Vector3 up = transformValue.GetUp();
        var direction = command.MovementX * right + command.MovementY * up + command.MovementZ * forward;

        //  Damping then input-driven velocity (matches the legacy floaty-motion feel).
        physicsValue.Velocity += -physicsValue.Velocity * PHYSICS_STEP * CharacterDefaults.DECELERATION;
        if (physicsValue.Velocity.LengthSquared() <= 0.00001f)
        {
            physicsValue.Velocity = new Vector3();
        }

        physicsValue.Velocity += direction * CharacterDefaults.BASE_SPEED * PHYSICS_STEP;
    }

    /// <summary>
    /// Drops look history for entities that no longer exist, so freed mirror indices do not reuse a
    /// stale look delta after a rejoin.
    /// </summary>
    private void PruneLookHistory()
    {
        if (_lastAppliedLook.Count == 0)
        {
            return;
        }

        List<int>? stale = null;
        foreach (int entity in _lastAppliedLook.Keys)
        {
            if (_store.GetUuid(entity) == Uuid.Null)
            {
                (stale ??= []).Add(entity);
            }
        }

        if (stale == null)
        {
            return;
        }

        foreach (int entity in stale)
        {
            _lastAppliedLook.Remove(entity);
        }
    }
}