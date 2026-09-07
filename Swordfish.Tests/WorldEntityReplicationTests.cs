using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Phase 2 (2.3): authoritative server world-body state replicates through the existing snapshot path and
/// the client view body snaps its drift. The shared codecs round-trip the server's physics-driven state;
/// a client copy deliberately seeded far off is pulled back to the authoritative transform.
/// </summary>
public class WorldEntityReplicationTests
{
    private const ulong TransformUuid = 2;
    private const ulong PhysicsUuid = 3;

    [Fact]
    public void WorldBodyStateReplicatesAndCorrectsDrift()
    {
        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<WorldSnapshot>() });

        var serverPhysics = new JoltPhysicsSystem(NullLogger<JoltPhysicsSystem>.Instance, new PhysicsSettings());
        serverPhysics.SetGravity(Vector3.Zero);

        var serverStore = new DataStore();
        int serverStruct = serverStore.Alloc();
        Uuid serverUuid = serverStore.GetUuid(serverStruct);
        serverStore.AddOrUpdate(serverStruct, new NetworkComponent());
        serverStore.AddOrUpdate(serverStruct, new TransformComponent(new Vector3(10f, 0f, 0f)));
        PhysicsComponent serverPhysicsComponent = PlayerBodyConfig.CreatePhysics();
        serverPhysicsComponent.Velocity = new Vector3(0f, 0f, 2f);
        serverStore.AddOrUpdate(serverStruct, serverPhysicsComponent);
        serverStore.AddOrUpdate(serverStruct, PlayerBodyConfig.CreateCollider(PlayerBodyConfig.PLAYER_SCALE));
        serverStore.AddOrUpdate(serverStruct, new ThrusterComponent(power: 2));

        var step = new SharedPlayerMotionStep(serverStore, serverPhysics, Resolver);

        //  Client view copy at the same uuid, deliberately drifted far from the authoritative pose.
        var clientStore = new DataStore();
        int clientStruct = clientStore.Alloc(serverUuid);
        clientStore.AddOrUpdate(clientStruct, new TransformComponent(new Vector3(999f, 999f, 999f)));
        clientStore.AddOrUpdate(clientStruct, PlayerBodyConfig.CreatePhysics());
        clientStore.AddOrUpdate(clientStruct, PlayerBodyConfig.CreateCollider(PlayerBodyConfig.PLAYER_SCALE));

        //  Repeatedly step the authority (which pushes the structure forward) and publish it; each applied
        //  snapshot must snap the client's drifted copy back to the server state.
        for (var i = 0; i < 3; i++)
        {
            for (var n = 0; n < 30; n++)
            {
                serverPhysics.Tick(0.016f, serverStore);
            }

            serverStore.TryGet(serverStruct, out TransformComponent authoritative);
            Publish(connection, serverStore, serverStruct, serverUuid);
            ApplyToClient(connection, clientStore, clientStruct);

            Assert.True(clientStore.TryGet(clientStruct, out TransformComponent client));
            Assert.True(Vector3.Distance(client.Position, authoritative.Position) < 1e-3f,
                $"Client world body should snap to the authoritative structure (client={client.Position}, server={authoritative.Position}).");
        }
    }

    private static void Publish(LocalConnection connection, DataStore serverStore, int entity, Uuid entityUuid)
    {
        var transformCodec = new TransformCodec();
        var physicsCodec = new PhysicsCodec();

        var snapshot = new WorldSnapshot
        {
            TickNumber = 1,
            LastProcessedInput = 0,
            Components =
            [
                new ComponentSnapshot(entityUuid.ToValue(), TransformUuid, transformCodec.Serialize(serverStore, entity)),
                new ComponentSnapshot(entityUuid.ToValue(), PhysicsUuid, physicsCodec.Serialize(serverStore, entity)),
            ],
            RemovedEntities = [],
        };

        connection.Server.Send(snapshot);
    }

    private static void ApplyToClient(LocalConnection connection, DataStore clientStore, int clientEntity)
    {
        Result<WorldSnapshot> received = connection.Client.Receive<WorldSnapshot>();
        Assert.True(received.Success);

        var transformCodec = new TransformCodec();
        var physicsCodec = new PhysicsCodec();

        ComponentSnapshot[] components = received.Value.Components;
        for (var i = 0; i < components.Length; i++)
        {
            if (components[i].TypeUuid == TransformUuid)
            {
                transformCodec.Apply(clientStore, clientEntity, components[i].Payload);
            }
            else if (components[i].TypeUuid == PhysicsUuid)
            {
                physicsCodec.Apply(clientStore, clientEntity, components[i].Payload);
            }
        }
    }

    private static bool Resolver(int entity, uint simTick, out InputComponent command)
    {
        command = default;
        return false;
    }
}