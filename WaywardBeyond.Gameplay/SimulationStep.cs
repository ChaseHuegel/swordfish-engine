using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Util;
using Swordfish.Physics;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Networking;

namespace WaywardBeyond.Gameplay;

/// <summary>The deterministic simulation step for an ECS world.</summary>
public sealed class SimulationStep : IDisposable
{
    /// <summary>Resolves the input command to apply for a given sim tick on a given entity.</summary>
    public delegate bool CommandResolver(int entity, uint simTick, out InputComponent command);

    private const int LOOK_PRUNE_INTERVAL = 64;

    private readonly DataStore _store;
    private readonly IPhysics _physics;
    private readonly CommandResolver _resolveCommand;
    private readonly uint _commandMaxAgeTicks;

    private readonly Dictionary<Uuid, Vector3> _lastAppliedLook = [];

    //  Forgets arrive between steps; queue them so the next step drops the totals before it applies
    //  commands. The queue also keeps the API safe if the caller and the step ever run on different threads.
    private readonly ConcurrentQueue<Uuid> _lookForgetRequests = new();

    private uint _stepsSinceLookPrune;

    /// <summary>The sim tick the next step applies; the server publishes it and reconcile aligns to it.</summary>
    public uint CurrentSimTick { get; private set; }

    /// <summary>The fixed timestep in seconds the physics solver and simulation step advance by.</summary>
    public float FixedDelta => _physics.FixedTimestamp;

    /// <summary>The maximum age of a staged input command a step applies, in sim ticks.</summary>
    public uint CommandMaxAgeTicks => _commandMaxAgeTicks;

    /// <summary>Re-seeds the sim-tick counter so replay continues from the server's tick.</summary>
    public void AlignTo(uint simTick)
    {
        CurrentSimTick = simTick;
    }

    /// <summary>Drops the last applied look totals for an entity identity before the next step.</summary>
    public void ForgetLookHistory(Uuid owner)
    {
        _lookForgetRequests.Enqueue(owner);
    }

    /// <summary>
    /// Creates the step bound to an ECS world's store and physics.
    /// <paramref name="resolveCommand"/> defaults to the store's staged input buffer.
    /// </summary>
    public SimulationStep(
        DataStore store,
        in IPhysics physics,
        CommandResolver? resolveCommand = null,
        uint commandMaxAgeTicks = uint.MaxValue
    ) {
        _store = store;
        _physics = physics;
        _commandMaxAgeTicks = commandMaxAgeTicks;
        _resolveCommand = resolveCommand ?? ((int entity, uint simTick, out InputComponent command) => ResolveStagedInput(_store, entity, simTick, _commandMaxAgeTicks, out command));
        physics.FixedUpdate += OnFixedUpdate;
    }

    /// <summary>Resolves the staged input for a sim tick from the store's <see cref="NetworkComponent"/> buffer.</summary>
    private static bool ResolveStagedInput(DataStore store, int entity, uint simTick, uint maxAgeTicks, out InputComponent command)
    {
        if (store.TryGet(entity, out NetworkComponent net) && net.StagedInputs != null)
        {
            Result<InputComponent> staged = net.StagedInputs.GetCommand(simTick, maxAgeTicks);
            if (staged.Success)
            {
                command = staged.Value;
                return true;
            }
        }

        command = default;
        return false;
    }

    /// <inheritdoc/>
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
        DrainLookForgets();
        CurrentSimTick++;

        if (++_stepsSinceLookPrune >= LOOK_PRUNE_INTERVAL)
        {
            _stepsSinceLookPrune = 0;
            PruneLookHistory();
        }

        StepAction action = new() { Owner = this };
        _store.QueryRef<InputComponent, PhysicsComponent, TransformComponent, StepAction>(0f, ref action);

        //  Character bodies without an InputComponent are remote player mirrors on the client. They get
        //  the same damping as the server's input-driven bodies, but never apply client input.
        RemoteBodyDampAction remoteAction = new() { Owner = this };
        _store.QueryRef<BodyViewComponent, PhysicsComponent, TransformComponent, RemoteBodyDampAction>(0f, ref remoteAction);
    }

    private void DrainLookForgets()
    {
        if (_lookForgetRequests.IsEmpty)
        {
            return;
        }

        while (_lookForgetRequests.TryDequeue(out Uuid owner))
        {
            _lastAppliedLook.Remove(owner);
        }
    }

    private struct StepAction : IForEachRef<InputComponent, PhysicsComponent, TransformComponent>
    {
        public SimulationStep Owner;

        public void Execute(float delta, DataStore store, int entity, ref Ref<InputComponent> input, ref Ref<PhysicsComponent> physics, ref Ref<TransformComponent> transform)
        {
            Owner.Execute(entity, ref physics, ref transform);
        }
    }

    private struct RemoteBodyDampAction : IForEachRef<BodyViewComponent, PhysicsComponent, TransformComponent>
    {
        public SimulationStep Owner;

        public void Execute(float delta, DataStore store, int entity, ref Ref<BodyViewComponent> body, ref Ref<PhysicsComponent> physics, ref Ref<TransformComponent> transform)
        {
            //  Input-driven bodies already damped in the command pass above.
            if (store.Has(typeof(InputComponent), entity))
            {
                return;
            }

            Owner.Damp(ref physics);
        }
    }

    private void Execute(int entity, ref Ref<PhysicsComponent> physics, ref Ref<TransformComponent> transform)
    {
        ref readonly TransformComponent transformValue = ref transform.Read;

        //  The solver's fixed timestep scales the per-step impulses; both sides run the same step.
        float fixedTimestamp = _physics.FixedTimestamp;

        //  Mirror the current values so an idle step (one that reproduces the current state) can skip
        //  the write. A Write marks the component dirty even when the value is unchanged, and the
        //  server replicates this component to every client.
        PhysicsComponent current = physics.Read;
        Vector3 torque = DampAngular(current.Torque, fixedTimestamp);
        Vector3 velocity = current.Velocity;

        //  The command for the current sim tick. Newest-for-tick collapse is handled by the resolver;
        //  a missing command damps linear velocity so a stalled stream slows to rest.
        if (_resolveCommand(entity, CurrentSimTick, out InputComponent command))
        {
            if (!command.DampenersDisabled)
            {
                velocity = DampLinear(velocity, fixedTimestamp);
            }

            //  Look: the wire carries absolute look totals; apply their per-sim-tick difference as
            //  angular velocity, decoupled from dt so each step rotates by the full accumulated delta.
            //  Totals are absolute per session, so the delta is tracked per entity identity: a recycled
            //  store index must never inherit a prior session's totals, and a join forgets the identity
            //  because a same-character rejoin reuses it.
            Uuid owner = _store.GetUuid(entity);
            var lookTotals = new Vector3(command.LookPitch, command.LookYaw, command.LookRoll);
            if (owner != Uuid.Null)
            {
                if (_lastAppliedLook.TryGetValue(owner, out Vector3 lastApplied))
                {
                    Vector3 perStep = lookTotals - lastApplied;
                    if (perStep != Vector3.Zero)
                    {
                        torque += Vector3.Transform(perStep / fixedTimestamp, transformValue.Orientation);
                    }
                }

                _lastAppliedLook[owner] = lookTotals;
            }

            //  Movement: derive world-space direction from the command and the current orientation.
            Vector3 forward = transformValue.GetForward();
            Vector3 right = transformValue.GetRight();
            Vector3 up = transformValue.GetUp();
            Vector3 direction = command.MovementX * right + command.MovementY * up + command.MovementZ * forward;

            velocity += direction * CharacterDefaults.BASE_SPEED * fixedTimestamp;
        }
        else
        {
            velocity = DampLinear(velocity, fixedTimestamp);
        }

        if (torque != current.Torque)
        {
            physics.Write.Torque = torque;
        }

        if (velocity != current.Velocity)
        {
            physics.Write.Velocity = velocity;
        }
    }

    /// <summary>Damps a body that applies no input, matching the command pass's commandless step.</summary>
    private void Damp(ref Ref<PhysicsComponent> physics)
    {
        float fixedTimestamp = _physics.FixedTimestamp;
        PhysicsComponent current = physics.Read;
        Vector3 torque = DampAngular(current.Torque, fixedTimestamp);
        Vector3 velocity = DampLinear(current.Velocity, fixedTimestamp);

        if (torque != current.Torque)
        {
            physics.Write.Torque = torque;
        }

        if (velocity != current.Velocity)
        {
            physics.Write.Velocity = velocity;
        }
    }

    /// <summary>Applies the per-step angular damping and snaps a resting body to zero.</summary>
    private static Vector3 DampAngular(Vector3 torque, float fixedTimestamp)
    {
        torque += -torque * fixedTimestamp * CharacterDefaults.ANGULAR_DECELERATION;
        if (torque.LengthSquared() <= CharacterDefaults.STOP_EPSILON)
        {
            torque = Vector3.Zero;
        }

        return torque;
    }

    /// <summary>Applies the per-step linear damping and snaps a resting body to zero.</summary>
    private static Vector3 DampLinear(Vector3 velocity, float fixedTimestamp)
    {
        velocity += -velocity * fixedTimestamp * CharacterDefaults.DECELERATION;
        if (velocity.LengthSquared() <= CharacterDefaults.STOP_EPSILON)
        {
            velocity = Vector3.Zero;
        }

        return velocity;
    }

    /// <summary>
    /// Drops look history for entity identities that no longer exist. Identity keys make a stale
    /// entry harmless, so this runs on an interval instead of every step.
    /// </summary>
    private void PruneLookHistory()
    {
        if (_lastAppliedLook.Count == 0)
        {
            return;
        }

        List<Uuid>? stale = null;
        foreach (Uuid owner in _lastAppliedLook.Keys)
        {
            if (!_store.TryGet(owner, out _))
            {
                (stale ??= []).Add(owner);
            }
        }

        if (stale == null)
        {
            return;
        }

        foreach (Uuid owner in stale)
        {
            _lastAppliedLook.Remove(owner);
        }
    }
}