#nullable enable
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Xunit;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Networking;

namespace Swordfish.Tests;

/// <summary>
/// Pins that the shared step tracks look totals by entity identity, so a recycled store index after
/// a rejoin never inherits the prior session's totals, and a join can forget a reused identity.
/// </summary>
public class SimulationStepLookHistoryTests
{
    [Fact]
    public void RecycledIndexDoesNotInheritLookTotals()
    {
        var store = new DataStore();
        using var step = new SimulationStep(store, new FakePhysics());

        int first = CreatePlayer(store);
        StageInput(store, first, new InputComponent { LookPitch = 50f, ServerTickAtSample = 1 });
        step.Step();

        store.Free(first);

        int second = CreatePlayer(store);
        Assert.Equal(first, second);

        StageInput(store, second, new InputComponent { LookPitch = 0f, ServerTickAtSample = 2 });
        step.Step();

        Assert.True(store.TryGet(second, out PhysicsComponent physics));
        Assert.Equal(Vector3.Zero, physics.Torque);
    }

    [Fact]
    public void ForgettingTheIdentityDropsThePriorSessionsLookTotals()
    {
        var store = new DataStore();
        using var step = new SimulationStep(store, new FakePhysics());

        int first = CreatePlayer(store);
        Uuid owner = store.GetUuid(first);
        StageInput(store, first, new InputComponent { LookPitch = 50f, ServerTickAtSample = 1 });
        step.Step();

        store.Free(first);

        int second = CreatePlayer(store, owner);
        Assert.Equal(owner, store.GetUuid(second));

        step.ForgetLookHistory(owner);
        StageInput(store, second, new InputComponent { LookPitch = 0f, ServerTickAtSample = 2 });
        step.Step();

        Assert.True(store.TryGet(second, out PhysicsComponent physics));
        Assert.Equal(Vector3.Zero, physics.Torque);
    }

    private static int CreatePlayer(DataStore store, Uuid uuid = default)
    {
        int entity = uuid == Uuid.Null ? store.Alloc() : store.Alloc(uuid);
        store.AddOrUpdate(entity, new InputComponent());
        store.AddOrUpdate(entity, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Discrete));
        store.AddOrUpdate(entity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(entity, NetworkComponent.Create());
        return entity;
    }

    private static void StageInput(DataStore store, int entity, in InputComponent input)
    {
        Assert.True(store.TryGet(entity, out NetworkComponent net));
        net.StagedInputs!.Add(input);
    }
}
