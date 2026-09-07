using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;
using WaywardBeyond.Shared.Gameplay;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests;

/// <summary>
/// Reproduces the rejoin crash: two Jolt worlds (client + server) step concurrently (the [E3] serialized
/// solve), and the client world tears down and rebuilds its bodies across sessions - dispose + free, then
/// re-create - while both worlds keep solving. This mirrors the client rebuilding its world on rejoin
/// against a concurrently-solved server world, which is where the game SIGSEGVs.
/// </summary>
public class RejoinConcurrencyTests
{
    private readonly ITestOutputHelper _output;

    public RejoinConcurrencyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ClientRebuildsWhileBothWorldsSolveDoesNotCrash()
    {
        var settings = new PhysicsSettings();
        var logger = NullLogger<JoltPhysicsSystem>.Instance;

        var clientPhysics = new JoltPhysicsSystem(logger, settings);
        var clientStore = new DataStore();
        var serverPhysics = new JoltPhysicsSystem(logger, settings);
        var serverStore = new DataStore();
        clientPhysics.SetGravity(Vector3.Zero);
        serverPhysics.SetGravity(Vector3.Zero);

        Task client = Task.Run(() =>
        {
            for (var session = 0; session < 4; session++)
            {
                BuildWorld(clientStore);
                for (var i = 0; i < 5; i++)
                {
                    clientPhysics.Tick(0.016f, clientStore);
                }

                DisposeAndFree(clientStore);
                for (var i = 0; i < 5; i++)
                {
                    clientPhysics.Tick(0.016f, clientStore);
                }
            }
        });

        Task server = Task.Run(() =>
        {
            BuildWorld(serverStore);
            for (var i = 0; i < 200; i++)
            {
                serverPhysics.Tick(0.016f, serverStore);
            }
        });

        Task combined = Task.WhenAll(client, server);
        if (Task.WhenAny(combined, Task.Delay(30000)).Result != combined)
        {
            Assert.True(false, "Client rebuild while both worlds solve hung.");
        }
    }

    private static void BuildWorld(DataStore store)
    {
        int player = store.Alloc();
        store.AddOrUpdate(player, new TransformComponent(new Vector3(0f, 1f, 0f), Quaternion.Identity, PlayerBodyConfig.PLAYER_SCALE));
        store.AddOrUpdate(player, PlayerBodyConfig.CreatePhysics());
        store.AddOrUpdate(player, PlayerBodyConfig.CreateCollider(PlayerBodyConfig.PLAYER_SCALE));
    }

    private static void DisposeAndFree(DataStore store)
    {
        DisposeAndFreeAction action = new();
        store.Query<PhysicsComponent, DisposeAndFreeAction>(0f, ref action);
    }

    private struct DisposeAndFreeAction : IForEach<PhysicsComponent>
    {
        public void Execute(float delta, DataStore store, int entity, in PhysicsComponent physics)
        {
            physics.Dispose();
            store.Free(entity);
        }
    }
}