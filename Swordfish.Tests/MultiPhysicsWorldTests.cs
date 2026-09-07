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
/// Regression coverage for hosting two <see cref="JoltPhysicsSystem"/> worlds in one process (the
/// server-authoritative layout: client physics on the ECS thread, server physics on the server thread).
/// JoltPhysicsSharp's <c>PhysicsSystem.Update(dt, steps, jobSystem)</c> overload funnels every solve
/// through one shared function-local temp allocator, so two worlds simulating on two threads concurrently
/// previously corrupted it and aborted (SIGABRT in <c>TempAllocatorImplWithMallocFallback::Free</c>).
/// The engine serializes native solves across worlds; this test drives both concurrently and must not
/// crash.
/// </summary>
public class MultiPhysicsWorldTests
{
    private readonly ITestOutputHelper _output;

    public MultiPhysicsWorldTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TwoConcurrentWorldsSteppingDoesNotAbort()
    {
        var settings = new PhysicsSettings();
        var logger = NullLogger<JoltPhysicsSystem>.Instance;

        var clientPhysics = new JoltPhysicsSystem(logger, settings);
        var clientStore = new DataStore();

        var serverPhysics = new JoltPhysicsSystem(logger, settings);
        var serverStore = new DataStore();

        int CreatePlayer(DataStore store, System.Numerics.Vector3 position)
        {
            int entity = store.Alloc();
            store.AddOrUpdate(entity, new TransformComponent(position));
            store.AddOrUpdate(entity, PlayerBodyConfig.CreatePhysics());
            store.AddOrUpdate(entity, PlayerBodyConfig.CreateCollider(PlayerBodyConfig.PLAYER_SCALE));
            return entity;
        }

        CreatePlayer(clientStore, PlayerBodyConfig.DEFAULT_SPAWN_POSITION);
        CreatePlayer(serverStore, PlayerBodyConfig.DEFAULT_SPAWN_POSITION);

        Task client = Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                clientPhysics.Tick(0.016f, clientStore);
            }
        });
        Task server = Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                serverPhysics.Tick(0.016f, serverStore);
            }
        });

        Task combined = Task.WhenAll(client, server);
        if (Task.WhenAny(combined, Task.Delay(20000)).Result != combined)
        {
            _output.WriteLine("Concurrent two-world stepping did not complete (possible deadlock).");
            Assert.True(false, "Concurrent two-world stepping hung.");
        }
    }
}