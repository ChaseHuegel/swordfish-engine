using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Server.Core.Permissions;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Server.Core.Systems;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;
using WaywardBeyond.Shared.Permissions;
using Xunit;

using WaywardBeyond.Shared.Config;

namespace Swordfish.Tests;

public class ServerJoinPermissionTests
{
    private static readonly INetworkSerializer[] Serializers =
    [
        new NsdMessageSerializer<JoinRequest>(),
        new NsdMessageSerializer<JoinAccept>(),
        new NsdMessageSerializer<LevelEntityAdd>(),
        new NsdMessageSerializer<LevelStreamComplete>(),
        new NsdMessageSerializer<WorldSnapshot>(),
        new NsdMessageSerializer<LeaveGameRequest>(),
    ];

    [Fact]
    public void JoinBindsTheClientClaimAndTreatsTheLoopbackAsHost()
    {
        (ServerJoinSystem join, UserPermissionService permissions, ServerConnectionHub hub) = CreateJoinSystem();

        var connection = new LocalConnection(Serializers);
        Uuid clientId = hub.Add(connection.Server);
        connection.Client.Send(new JoinRequest
        {
            CharacterId = 1,
            PublicView = new PublicView { CharacterId = 1, Name = "Host", Body = "wb:m_human" },
            UserId = "host-user",
        });

        join.Tick(0f, new DataStore());

        Assert.True(permissions.TryGetClaim(clientId, out UserClaim claim));
        Assert.Equal("host-user", claim.UserId);
        Assert.True(permissions.IsHost(clientId));
        Assert.True(permissions.HasPermission(clientId, "any.permission"));
    }

    [Fact]
    public void LeaveUnbindsTheClientClaim()
    {
        (ServerJoinSystem join, UserPermissionService permissions, ServerConnectionHub hub) = CreateJoinSystem();

        var connection = new LocalConnection(Serializers);
        Uuid clientId = hub.Add(connection.Server);
        connection.Client.Send(new JoinRequest
        {
            CharacterId = 1,
            PublicView = new PublicView { CharacterId = 1, Name = "Host", Body = "wb:m_human" },
            UserId = "host-user",
        });

        var store = new DataStore();
        join.Tick(0f, store);
        Assert.True(permissions.TryGetClaim(clientId, out _));

        connection.Client.Send(new LeaveGameRequest { Dummy = 0 });
        join.Tick(0f, store);

        Assert.False(permissions.TryGetClaim(clientId, out _));
    }

    private static (ServerJoinSystem Join, UserPermissionService Permissions, ServerConnectionHub Hub) CreateJoinSystem()
    {
        var hub = new ServerConnectionHub();
        var sessions = new SessionManager();
        var permissions = new UserPermissionService(PermissionPolicy.Create([]));
        var replication = new NetworkReplicationSystem(hub, sessions, NullLogger<NetworkReplicationSystem>.Instance, new NetworkingSettings());
        var level = new LevelSaveService(NullLogger<LevelSaveService>.Instance, new StubLevelCatalog(), TestBricks.Map);
        var join = new ServerJoinSystem(
            hub,
            sessions,
            level,
            replication,
            TestInteractionSystem.Create(hub),
            NullLogger<ServerJoinSystem>.Instance,
            TestBricks.Map,
            permissions: permissions
        );
        return (join, permissions, hub);
    }
}
