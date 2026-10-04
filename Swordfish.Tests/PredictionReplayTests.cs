using System;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Physics;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// The client replay path must reproduce the server's command-per-sim-tick sequence exactly: the same
/// pending input sequence, resolved via newest-per-sim-tick, converges to the same state on both sides.
/// This pins the contract that networking-prediction.md documents as "same input sequence -> same state".
/// </summary>
public class PredictionReplayTests
{
    private sealed class StubPhysics : IPhysics
    {
        public event EventHandler<EventArgs>? FixedUpdate;
        public RaycastResult Raycast(in Ray ray) => default;
        public void SetGravity(Vector3 gravity) { }
    }

    [Fact]
    public void ClientReplayMatchesServerStagingFromSameSequence()
    {
        //  The same sampled sequence on both sides, including a same-tick pair (tags 2 and 2): the
        //  newest of the pair must win on both sides.
        InputComponent[] samples =
        [
            new InputComponent { MovementX = 1f, LookYaw = 0.1f, SequenceNumber = 1, ServerTickAtSample = 0 },
            new InputComponent { MovementX = 0f, LookYaw = 0.3f, SequenceNumber = 2, ServerTickAtSample = 1 },
            new InputComponent { MovementZ = -1f, LookYaw = 0.4f, SequenceNumber = 3, ServerTickAtSample = 2 },
            new InputComponent { MovementZ = 2f, LookYaw = 0.5f, SequenceNumber = 4, ServerTickAtSample = 2 },
            new InputComponent { MovementX = -1f, LookYaw = 0.2f, SequenceNumber = 5, ServerTickAtSample = 4 },
        ];

        //  Server side: commands staged in an InputStageBuffer (the server's staging path).
        var serverStore = new DataStore();
        int serverEntity = serverStore.Alloc();
        serverStore.AddOrUpdate(serverEntity, new InputComponent());
        serverStore.AddOrUpdate(serverEntity, new PhysicsComponent());
        serverStore.AddOrUpdate(serverEntity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        var staged = new InputStageBuffer();
        foreach (InputComponent sample in samples)
        {
            staged.Stage(sample);
        }
        var serverStep = new SharedSimulationStep(serverStore, new StubPhysics(), (int entity, uint simTick, out InputComponent command) =>
            staged.TryGet(simTick, out command));

        //  Client side: the same sequence pushed into the pending ring, resolved by the replay lookup.
        var clientStore = new DataStore();
        int clientEntity = clientStore.Alloc();
        clientStore.AddOrUpdate(clientEntity, new InputComponent());
        clientStore.AddOrUpdate(clientEntity, new PhysicsComponent());
        clientStore.AddOrUpdate(clientEntity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        var pending = new PendingInputComponent();
        foreach (InputComponent sample in samples)
        {
            pending.Push(sample);
        }
        clientStore.AddOrUpdate(clientEntity, pending);
        var clientStep = new SharedSimulationStep(clientStore, new StubPhysics(), (int entity, uint simTick, out InputComponent command) =>
            pending.TryGetNewestAtOrBefore(simTick, out command));

        //  Step both sides in lockstep; state must match at every step.
        try
        {
            for (var step = 1; step <= 6; step++)
            {
                serverStep.Step();
                clientStep.Step();

                Assert.True(serverStore.TryGet(serverEntity, out PhysicsComponent serverPhysics));
                Assert.True(clientStore.TryGet(clientEntity, out PhysicsComponent clientPhysics));
                Assert.Equal(serverPhysics.Velocity, clientPhysics.Velocity);
                Assert.Equal(serverPhysics.Torque, clientPhysics.Torque);
            }
        }
        finally
        {
            serverStep.Dispose();
            clientStep.Dispose();
        }
    }
}