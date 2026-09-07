using System;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Phase 2 (2.3): world/voxel bodies are Dynamic and the shared structure dynamics run in the shared
/// deterministic step on both the server authority world and the client prediction world. Given the same
/// identities, the two worlds must produce identical motion.
/// </summary>
public class SharedStructureDynamicsTests
{
    [Fact]
    public void ThrusterDrivenStructureMovesIdenticallyAcrossTwoWorlds()
    {
        var first = new DataStore();
        var second = new DataStore();
        CreateStructure(first, new Vector3(0f, 0f, 0f), power: 3, velocity: Vector3.Zero);
        CreateStructure(second, new Vector3(0f, 0f, 0f), power: 3, velocity: Vector3.Zero);

        StepBoth(first, second, steps: 400);

        AssertPosesMatch(first, second);
    }

    [Fact]
    public void ImpulsePushedStructureMovesIdenticallyAcrossTwoWorlds()
    {
        var first = new DataStore();
        var second = new DataStore();
        CreateStructure(first, new Vector3(0f, 0f, 0f), power: 0, velocity: new Vector3(0f, 0f, 6f));
        CreateStructure(second, new Vector3(0f, 0f, 0f), power: 0, velocity: new Vector3(0f, 0f, 6f));

        StepBoth(first, second, steps: 400);

        AssertPosesMatch(first, second);
    }

    private static void StepBoth(DataStore first, DataStore second, int steps)
    {
        var physicsA = new JoltPhysicsSystem(NullLogger<JoltPhysicsSystem>.Instance, new PhysicsSettings());
        physicsA.SetGravity(Vector3.Zero);
        var stepA = new SharedPlayerMotionStep(first, physicsA, Resolver);

        var physicsB = new JoltPhysicsSystem(NullLogger<JoltPhysicsSystem>.Instance, new PhysicsSettings());
        physicsB.SetGravity(Vector3.Zero);
        var stepB = new SharedPlayerMotionStep(second, physicsB, Resolver);

        for (var i = 0; i < steps; i++)
        {
            physicsA.Tick(0.016f, first);
            physicsB.Tick(0.016f, second);
        }
    }

    private static void AssertPosesMatch(DataStore first, DataStore second)
    {
        var positions = new Vector3[2];
        first.Query<TransformComponent>(0f, (float _, DataStore store, int entity, in TransformComponent t) => positions[0] = t.Position);
        second.Query<TransformComponent>(0f, (float _, DataStore store, int entity, in TransformComponent t) => positions[1] = t.Position);

        Assert.True(Vector3.Distance(positions[0], positions[1]) < 1e-4f,
            $"Structures diverged: server={positions[0]}, client={positions[1]}.");
        Assert.True(positions[0].LengthSquared() > 0.01f,
            "The structure should have moved from the origin (impulse/thrust applied).");
    }

    private static void CreateStructure(DataStore store, Vector3 position, int power, Vector3 velocity)
    {
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TransformComponent(position));
        PhysicsComponent physics = PlayerBodyConfig.CreatePhysics();
        physics.Velocity = velocity;
        store.AddOrUpdate(entity, physics);
        store.AddOrUpdate(entity, PlayerBodyConfig.CreateCollider(PlayerBodyConfig.PLAYER_SCALE));
        store.AddOrUpdate(entity, new ThrusterComponent(power));
    }

    private static bool Resolver(int entity, uint simTick, out InputComponent command)
    {
        command = default;
        return false;
    }
}