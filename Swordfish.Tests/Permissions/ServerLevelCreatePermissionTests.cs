using System;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Util;
using Swordfish.Library.Serialization.Toml;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Data;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Menu-time level creation is gated by <c>waywardbeyond.level.create</c>. The local host is always
/// allowed. A remote connection may only create after its <see cref="ClientHello"/> claim is granted
/// the key, and the create capability rides back on the level listing.
/// </summary>
public class ServerLevelCreatePermissionTests
{
    private static readonly INetworkSerializer[] Serializers =
    [
        new NsdMessageSerializer<ClientHello>(),
        new NsdMessageSerializer<NewLevelRequest>(),
        new NsdMessageSerializer<NewLevelResponse>(),
        new NsdMessageSerializer<ListLevelsRequest>(),
        new NsdMessageSerializer<ListLevelsResponse>(),
    ];

    [Fact]
    public void RemoteConnectionWithoutGrantIsDenied()
    {
        var catalog = new RecordingLevelCatalog();
        var pendingJoins = new PendingJoins();
        ServerLevelManager manager = CreateManager(pendingJoins, catalog, TestPermissions.EmptyPolicy);

        var connection = new LocalConnection(Serializers);
        pendingJoins.Add(new NonLocalServerConnection(connection.Server));

        connection.Client.Send(new ClientHello { UserId = "guest" });
        connection.Client.Send(new NewLevelRequest { Name = "T", Seed = "s", GameMode = 0 });

        manager.Tick();

        Assert.False(WaitForCreate(connection).Success);
        Assert.False(catalog.Created);
    }

    [Fact]
    public void RemoteConnectionWithGrantMayCreate()
    {
        var catalog = new RecordingLevelCatalog();
        var pendingJoins = new PendingJoins();
        ServerLevelManager manager = CreateManager(pendingJoins, catalog, PolicyGranting("builder"));

        var connection = new LocalConnection(Serializers);
        pendingJoins.Add(new NonLocalServerConnection(connection.Server));

        connection.Client.Send(new ClientHello { UserId = "builder" });
        connection.Client.Send(new NewLevelRequest { Name = "T", Seed = "s", GameMode = 0 });

        manager.Tick();

        Assert.True(WaitForCreate(connection).Success);
        Assert.True(catalog.Created);
    }

    [Fact]
    public void ListLevelsReportsCreateCapability()
    {
        var pendingJoins = new PendingJoins();
        ServerLevelManager manager = CreateManager(pendingJoins, new RecordingLevelCatalog(), PolicyGranting("builder"));

        var granted = new LocalConnection(Serializers);
        var guest = new LocalConnection(Serializers);
        pendingJoins.Add(new NonLocalServerConnection(granted.Server));
        pendingJoins.Add(new NonLocalServerConnection(guest.Server));

        granted.Client.Send(new ClientHello { UserId = "builder" });
        guest.Client.Send(new ClientHello { UserId = "guest" });
        granted.Client.Send(new ListLevelsRequest { Dummy = 0 });
        guest.Client.Send(new ListLevelsRequest { Dummy = 0 });

        manager.Tick();

        Assert.True(WaitForListing(granted).CanCreateSave);
        Assert.False(WaitForListing(guest).CanCreateSave);
    }

    [Fact]
    public void LocalConnectionMayCreateWithoutGrant()
    {
        var catalog = new RecordingLevelCatalog();
        var pendingJoins = new PendingJoins();
        ServerLevelManager manager = CreateManager(pendingJoins, catalog, TestPermissions.EmptyPolicy);

        var connection = new LocalConnection(Serializers);
        pendingJoins.Add(connection.Server);

        connection.Client.Send(new NewLevelRequest { Name = "T", Seed = "s", GameMode = 0 });

        manager.Tick();

        Assert.True(WaitForCreate(connection).Success);
        Assert.True(catalog.Created);
    }

    private static ServerLevelManager CreateManager(PendingJoins pendingJoins, ILevelCatalog catalog, IPermissionPolicy policy)
    {
        return new ServerLevelManager(pendingJoins, new PendingLevelDeletes(), catalog, policy, new ConnectionClaims(), NullLoggerFactory.Instance);
    }

    private static IPermissionPolicy PolicyGranting(string userId)
    {
        return PermissionPolicy.Create([
            new SourcedPermissionFile("test.toml", Toml.To<PermissionFile>($"""
                [Groups.creator]
                Permissions = ["{GamePermissions.LevelCreate}"]

                [Users."{userId}"]
                Roles = ["creator"]
                """))
        ]);
    }

    private static NewLevelResponse WaitForCreate(LocalConnection connection, int timeoutMs = 5000)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            Result<NewLevelResponse> response = connection.Client.Receive<NewLevelResponse>();
            if (response.Success)
            {
                return response.Value;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("No create response arrived.");
    }

    private static ListLevelsResponse WaitForListing(LocalConnection connection, int timeoutMs = 5000)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            Result<ListLevelsResponse> response = connection.Client.Receive<ListLevelsResponse>();
            if (response.Success)
            {
                return response.Value;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("No level listing arrived.");
    }

    /// <summary>Wraps a local endpoint as a remote connection so <c>IsLocal</c> is false.</summary>
    private sealed class NonLocalServerConnection(IServerConnection inner) : IServerConnection
    {
        public bool IsConnected => inner.IsConnected;
        public bool IsLocal => false;
        public Result Send<T>(in T message) => inner.Send(message);
        public Result<T> Receive<T>() => inner.Receive<T>();
        public Result SendRaw(in byte[] frame) => inner.SendRaw(frame);
    }

    private sealed class RecordingLevelCatalog : ILevelCatalog
    {
        public bool Created { get; private set; }

        public bool Create(string name, string seed, GameMode gameMode, out string levelGuid)
        {
            Created = true;
            levelGuid = "level-guid";
            return true;
        }

        public Level[] ListLevels() => [];

        public bool Delete(string levelGuid) => true;

        public bool Exists(string levelGuid) => false;

        public ILevelStore? Open(string levelGuid) => null;
    }
}
