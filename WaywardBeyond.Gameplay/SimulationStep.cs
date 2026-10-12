using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Physics;
using WaywardBeyond.Networking.Components;

namespace WaywardBeyond.Gameplay;

/// <summary>The deterministic simulation step for an ECS world.</summary>
public sealed class SimulationStep : IDisposable
{
    /// <summary>The fixed physics step (seconds) the step runs at, matching <c>JoltPhysicsSystem</c>.</summary>
    private const float PHYSICS_STEP = 0.016f;

    /// <summary>Resolves the input command to apply for a given sim tick on a given entity.</summary>
    public delegate bool CommandResolver(int entity, uint simTick, out InputComponent command);

    private readonly DataStore _store;
    private readonly IPhysics _physics;
    private readonly CommandResolver _resolveCommand;

    private readonly Dictionary<int, Vector3> _lastAppliedLook = [];

    /// <summary>The sim tick the next step applies; the server publishes it and reconcile aligns to it.</summary>
    public uint CurrentSimTick { get; private set; }

    /// <summary>Re-seeds the sim-tick counter so replay continues from the server's tick.</summary>
    public void AlignTo(uint simTick)
    {
        CurrentSimTick = simTick;
    }

    /// <summary>
    /// Creates the step bound to an ECS world's store and physics.
    /// <paramref name="resolveCommand"/> defaults to the store's staged input buffer.
    /// </summary>
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

    /// <summary>Advances the sim tick and applies the command for it to every input-driven body.</summary>
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

        //  Look: the wire carries absolute look totals; apply their per-sim-tick difference as torque,
        //  decoupled from dt so each step rotates by the full accumulated delta.
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