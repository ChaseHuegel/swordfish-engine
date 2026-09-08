using System;
using System.Collections.Generic;
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
using WaywardBeyond.Shared.Networking.Sessions;
using WaywardBeyond.Shared.Networking.Transport;
using Xunit;

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
        new NsdMessageSerializer<WorldStreamComplete>(),
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
            new WorldSaveService(NullLogger<WorldSaveService>.Instance, () => throw new NotImplementedException()),
            NullLogger<ServerJoinSystem>.Instance
        );

        const ulong characterId = 42;
        fixture.Client(0).Send(new JoinRequest
        {
            CharacterId = characterId,
            PublicView = new PublicView { CharacterId = characterId, Name = "Ada", Body = 2 },
        });

        system.Tick(0f, fixture.Store);

        //  The player mirror is bound to the session and relays the public view: the appearance index
        //  (for the billboard) and the name on the reused IdentifierComponent, both cloned from the request.
        Assert.True(fixture.Sessions.TryGetEntity(fixture.ClientIds[0], out int entity));

        Assert.True(fixture.Store.TryGet(entity, out BodyViewComponent body));
        Assert.Equal(2, body.Body);

        Assert.True(fixture.Store.TryGet(entity, out IdentifierComponent identifier));
        Assert.Equal("Ada", identifier.Name);
        Assert.Equal(PlayerBodyConfig.PLAYER_TAG, identifier.Tag);
    }

    [Fact]
    public void IdentifierComponentRegistersAsServerOwned()
    {
        Assert.True(NetworkRegistry.TryGetInfo(Uuid.FromValue(IdentifierUuid), out NetworkComponentInfo info));
        Assert.Equal(typeof(IdentifierComponent), info.Codec.ComponentType);
        Assert.Equal(NetworkDirection.ServerOwned, info.Direction);
    }
}