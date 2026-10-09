using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Sessions;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// The server owns the save cadence and authorizes manual save requests. These tests drive a real
/// SQLite level, a hub, and a world system.
/// </summary>
public class ServerWorldSaveTests : IDisposable
{
    private static readonly INetworkSerializer[] Serializers =
    [
        new NsdMessageSerializer<SaveLevelRequest>(),
        new NsdMessageSerializer<SaveLevelResponse>(),
        new NsdMessageSerializer<NotificationMessage>(),
    ];

    private readonly LevelFixture _fixture = new();
    private readonly DataStore _store = new();

    public void Dispose()
    {
        _fixture.Dispose();
    }

    [Fact]
    public void AutosaveQueuesAndReportsTheSave()
    {
        bool created = _fixture.Catalog.Create("Autosave Level", "1", GameMode.Creative, out string guid);
        Assert.True(created);

        World world = CreateWorld(guid);
        RegisterSession(world.Sessions, world.ClientId);

        world.System.Tick(1.1f, _store);
        Assert.True(DrainHasKey(world.Connection, "notification.save.saving"), "Autosave must broadcast its start.");

        Assert.True(WaitForKey(world.System, world.Connection, "notification.save.saved"), "Autosave must broadcast its completion.");
        Assert.False(world.Save.TryDequeueCompletion(out _), "The world system must drain every completion.");
    }

    [Fact]
    public void AutosaveIsSkippedWithoutSessions()
    {
        bool created = _fixture.Catalog.Create("Idle Level", "1", GameMode.Creative, out string guid);
        Assert.True(created);

        World world = CreateWorld(guid);

        world.System.Tick(10f, _store);

        Assert.False(DrainHasKey(world.Connection, "notification.save.saving"), "An idle world must not autosave.");
    }

    [Fact]
    public void HostSaveRequestIsAccepted()
    {
        bool created = _fixture.Catalog.Create("Host Level", "1", GameMode.Creative, out string guid);
        Assert.True(created);

        World world = CreateWorld(guid);
        RegisterSession(world.Sessions, world.ClientId);
        world.Permissions.Bind(world.ClientId, UserClaim.Anonymous, isHost: true);

        world.Connection.Client.Send(new SaveLevelRequest());
        world.System.Tick(0f, _store);

        Result<SaveLevelResponse> response = world.Connection.Client.Receive<SaveLevelResponse>();
        Assert.True(response.Success);
        Assert.True(response.Value.Success);
        Assert.True(DrainHasKey(world.Connection, "notification.save.saving"));
    }

    [Fact]
    public void UnpermittedSaveRequestIsDeniedAndNotified()
    {
        bool created = _fixture.Catalog.Create("Denied Level", "1", GameMode.Creative, out string guid);
        Assert.True(created);

        World world = CreateWorld(guid);
        RegisterSession(world.Sessions, world.ClientId);
        world.Permissions.Bind(world.ClientId, new UserClaim("nobody"), isHost: false);

        world.Connection.Client.Send(new SaveLevelRequest());
        world.System.Tick(0f, _store);

        Result<SaveLevelResponse> response = world.Connection.Client.Receive<SaveLevelResponse>();
        Assert.True(response.Success);
        Assert.False(response.Value.Success, "A client without the save permission must be refused.");
        Assert.True(DrainHasKey(world.Connection, "notification.save.denied"), "The refused client must be told why.");
        Assert.False(DrainHasKey(world.Connection, "notification.save.saving"), "A refused request must not queue a save.");
        Assert.False(world.Save.TryDequeueCompletion(out _), "A refused request must not queue a save.");
    }

    [Fact]
    public void LevelSaveCompletionReportsSuccess()
    {
        bool created = _fixture.Catalog.Create("Completion Level", "1", GameMode.Creative, out string guid);
        Assert.True(created);

        World world = CreateWorld(guid);

        world.Save.QueueSave(_store);

        Assert.True(WaitForCompletion(world.Save, out bool success), "A queued save must report a completion.");
        Assert.True(success, "The write must succeed against a real level database.");
    }

    [Fact]
    public void SaveLevelRequestRidesTheReliableQueue()
    {
        Assert.True(SendPriority.IsReliable(typeof(SaveLevelRequest)));
    }

    [Fact]
    public void LevelListingRequestsRideTheReliableQueue()
    {
        //  A dropped create/list/delete silently loses a save operation on a remote client, so every
        //  menu-time level-management message must never be evicted from the send queue.
        Assert.True(SendPriority.IsReliable(typeof(NewLevelRequest)));
        Assert.True(SendPriority.IsReliable(typeof(ListLevelsRequest)));
        Assert.True(SendPriority.IsReliable(typeof(DeleteLevelRequest)));
        Assert.True(SendPriority.IsReliable(typeof(NewLevelResponse)));
        Assert.True(SendPriority.IsReliable(typeof(ListLevelsResponse)));
        Assert.True(SendPriority.IsReliable(typeof(DeleteLevelResponse)));
    }

    private World CreateWorld(string levelGuid)
    {
        var settings = new GameplaySettings();
        settings.AutosaveIntervalMs.Set(1000);

        var save = new LevelSaveService(NullLogger<LevelSaveService>.Instance, _fixture.Catalog, TestBricks.Map);
        Assert.True(save.LoadLevel(levelGuid, _store));

        var hub = new ServerConnectionHub();
        var sessions = new SessionManager();
        var permissions = new UserPermissionService(PermissionPolicy.Create([]));
        var system = new ServerWorldSystem(hub, sessions, save, permissions, settings, NullLogger<ServerWorldSystem>.Instance);

        var connection = new LocalConnection(Serializers);
        Uuid clientId = hub.Add(connection.Server);

        return new World(system, save, hub, sessions, permissions, connection, clientId, settings);
    }

    private void RegisterSession(SessionManager sessions, Uuid clientId)
    {
        int entity = _store.Alloc();
        _store.AddOrUpdate(entity, new NetworkComponent());
        sessions.Register(_store, entity, clientId, new Session(1));
    }

    private bool WaitForKey(ServerWorldSystem system, LocalConnection connection, string key)
    {
        int deadline = Environment.TickCount + 5000;
        while (Environment.TickCount < deadline)
        {
            system.Tick(0f, _store);
            if (DrainHasKey(connection, key))
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return false;
    }

    private static bool DrainHasKey(LocalConnection connection, string key)
    {
        var found = false;
        Result<NotificationMessage> result;
        while ((result = connection.Client.Receive<NotificationMessage>()).Success)
        {
            found |= result.Value.Key == key;
        }

        return found;
    }

    private static bool WaitForCompletion(LevelSaveService save, out bool success)
    {
        int deadline = Environment.TickCount + 5000;
        while (Environment.TickCount < deadline)
        {
            if (save.TryDequeueCompletion(out success))
            {
                return true;
            }

            Thread.Sleep(5);
        }

        success = false;
        return false;
    }

    private sealed class World(
        ServerWorldSystem system,
        LevelSaveService save,
        ServerConnectionHub hub,
        SessionManager sessions,
        UserPermissionService permissions,
        LocalConnection connection,
        Uuid clientId,
        GameplaySettings settings
    ) {
        public readonly ServerWorldSystem System = system;
        public readonly LevelSaveService Save = save;
        public readonly ServerConnectionHub Hub = hub;
        public readonly SessionManager Sessions = sessions;
        public readonly UserPermissionService Permissions = permissions;
        public readonly LocalConnection Connection = connection;
        public readonly Uuid ClientId = clientId;
        public readonly GameplaySettings Settings = settings;
    }

    private sealed class LevelFixture : IDisposable
    {
        private readonly string _dataRoot;

        public ILevelCatalog Catalog { get; }

        public LevelFixture()
        {
            _dataRoot = Path.Combine(Path.GetTempPath(), "wb_world_save_" + Guid.NewGuid().ToString("N"));
            var settings = new StorageSettings();
            settings.DataRoot.Set(_dataRoot);
            var paths = new StoragePaths(settings);
            Catalog = new SqliteLevelCatalog(NullLogger<SqliteLevelCatalog>.Instance, paths, TestBricks.Map);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_dataRoot))
                {
                    Directory.Delete(_dataRoot, recursive: true);
                }
            }
            catch
            {
                //  Best-effort teardown.
            }
        }
    }
}
