using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Sessions;
using WaywardBeyond.Networking.Transport;
using Xunit;

using WaywardBeyond.Config;

namespace Swordfish.Tests;

/// <summary>
/// Phase 2.2 remote-player visuals: the server stores the joining client's minimal public character view
/// on the player mirror as a replicated <see cref="BodyViewComponent"/> (appearance index) plus a name on
/// the reused engine <see cref="IdentifierComponent"/>, so remote clients can materialize the player.
/// </summary>
public class RemotePlayerVisualTests
{
    //  Unique uuid so it avoids colliding with other tests' ad hoc registrations in the shared NetworkRegistry.
    private const ulong IdentifierUuid = 0xE011;

    public RemotePlayerVisualTests()
    {
        NetworkRegistry.Register<IdentifierComponent>(Uuid.FromValue(IdentifierUuid), NetworkDirection.ServerOwned, new IdentifierCodec());
    }

    private static INetworkSerializer[] Serializers => new INetworkSerializer[]
    {
        new NsdMessageSerializer<JoinRequest>(),
        new NsdMessageSerializer<JoinAccept>(),
        new NsdMessageSerializer<LevelStreamComplete>(),
        new NsdMessageSerializer<WorldSnapshot>(),
        new NsdMessageSerializer<LeaveGameRequest>(),
    };

    private sealed class Fixture
    {
        public ServerConnectionHub Hub { get; } = new();
        public SessionManager Sessions { get; } = new();
        public DataStore Store { get; } = new();
        public List<LocalConnection> Connections { get; } = [];
        public List<Uuid> ClientIds { get; } = [];

        public Fixture(int clientCount)
        {
            for (var i = 0; i < clientCount; i++)
            {
                var connection = new LocalConnection(Serializers);
                Connections.Add(connection);
                ClientIds.Add(Hub.Add(connection.Server));
            }
        }

        public IClientConnection Client(int i) => Connections[i].Client;
    }

    [Fact]
    public void JoinStoresPublicViewOnTheServerMirror()
    {
        Fixture fixture = new(1);
        var system = new ServerJoinSystem(
            fixture.Hub,
            fixture.Sessions,
            new LevelSaveService(NullLogger<LevelSaveService>.Instance, new StubLevelCatalog(), TestBricks.Map),
            new NetworkReplicationSystem(fixture.Hub, fixture.Sessions, NullLogger<NetworkReplicationSystem>.Instance, new NetworkingSettings()),
            TestInteractionSystem.Create(fixture.Hub),
            NullLogger<ServerJoinSystem>.Instance,
            TestBricks.Map
        );

        const ulong characterId = 42;
        fixture.Client(0).Send(new JoinRequest
        {
            CharacterId = characterId,
            PublicView = new PublicView { CharacterId = characterId, Name = "Ada", Body = "wb:m_human" },
        });

        system.Tick(0f, fixture.Store);

        //  The player mirror is bound to the session and relays the public view: the appearance index
        //  (for the billboard) and the name on the reused IdentifierComponent, both cloned from the request.
        Assert.True(fixture.Sessions.TryGetEntity(fixture.ClientIds[0], out int entity));

        Assert.True(fixture.Store.TryGet(entity, out BodyViewComponent body));
        Assert.Equal("wb:m_human", body.Body);

        Assert.True(fixture.Store.TryGet(entity, out IdentifierComponent identifier));
        Assert.Equal("Ada", identifier.Name);
        Assert.Equal("game", identifier.Tag);
    }

    [Fact]
    public void IdentifierComponentRegistersAsServerOwned()
    {
        Assert.True(NetworkRegistry.TryGetInfo(Uuid.FromValue(IdentifierUuid), out NetworkComponentInfo info));
        Assert.Equal(typeof(IdentifierComponent), info.Codec.ComponentType);
        Assert.Equal(NetworkDirection.ServerOwned, info.Direction);
    }
}