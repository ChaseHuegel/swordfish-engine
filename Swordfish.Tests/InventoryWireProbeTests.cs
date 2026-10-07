using System;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Server.Core.Systems;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;
using Xunit;

using WaywardBeyond.Shared.Config;

namespace Swordfish.Tests;

public class InventoryWireProbeTests
{
    [Fact]
    public void ServerEchoCarriesStarterInventory()
    {
        NetworkRegistry.Initialize([typeof(InputComponent).Assembly]);

        var serializers = new INetworkSerializer[]
        {
            new NsdMessageSerializer<JoinRequest>(),
            new NsdMessageSerializer<JoinAccept>(),
            new NsdMessageSerializer<LevelEntityAdd>(),
            new NsdMessageSerializer<LevelStreamComplete>(),
            new NsdMessageSerializer<WorldSnapshot>(),
        };
        var connection = new LocalConnection(serializers);
        var hub = new ServerConnectionHub();
        Uuid clientId = hub.Add(connection.Server);
        var sessions = new SessionManager();
        var store = new DataStore();

        var replication = new NetworkReplicationSystem(hub, sessions, NullLogger<NetworkReplicationSystem>.Instance, new NetworkingSettings());
        var interaction = TestInteractionSystem.Create(hub);
        var join = new ServerJoinSystem(
            hub,
            sessions,
            new LevelSaveService(NullLogger<LevelSaveService>.Instance, new StubLevelCatalog(), TestBricks.Map),
            replication,
            interaction,
            NullLogger<ServerJoinSystem>.Instance,
            TestBricks.Map
        );

        connection.Client.Send(new JoinRequest
        {
            CharacterId = 100,
            PublicView = new PublicView { CharacterId = 100, Name = "P", Body = "wb:m_human" },
        });

        join.Tick(0f, store);
        replication.ApplyStage(0f, store);
        replication.SimTick = 1;
        replication.PublishStage(1f, store);

        Result<WorldSnapshot> snapshot = connection.Client.Receive<WorldSnapshot>();
        Assert.True(snapshot.Success);
        bool seenInventory = false;
        foreach (ComponentSnapshot component in snapshot.Value.Components)
        {
            if (NetworkRegistry.TryGetInfo(Uuid.FromValue(component.TypeUuid), out NetworkComponentInfo info)
                && info.Type == typeof(InventoryComponent))
            {
                seenInventory = true;
                InventoryComponent inventory = InventoryComponent.Deserialize(component.Payload);
                Assert.Contains(inventory.Contents, item => item.ID == "laser" && item.Count > 0);
            }
        }

        Assert.True(seenInventory, "The first echo must carry the InventoryComponent.");
    }
}