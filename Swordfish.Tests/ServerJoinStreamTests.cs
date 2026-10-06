using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Server.Core.Systems;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Sessions;
using WaywardBeyond.Shared.Networking.Transport;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Headless integration coverage of the server-owned level and the join/full-level-stream path, backed
/// by real SQLite save databases in a throwaway data root (so persistence, not a mock, is exercised).
/// </summary>
public class ServerJoinStreamTests : IDisposable
{
    private readonly LevelFixture _fixture;

    public ServerJoinStreamTests()
    {
        _fixture = new LevelFixture();
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    [Fact]
    public void CreateLevelThenLoadBuildsAuthoritativeBodies()
    {
        ILevelCatalog catalog = _fixture.Catalog;
        LevelSaveService level = new(NullLogger<LevelSaveService>.Instance, catalog, TestBricks.Map);

        bool created = catalog.Create("Test Level", seed: "1337", GameMode.Creative, out string guid);
        Assert.True(created);
        Assert.False(string.IsNullOrEmpty(guid));

        Level[] levels = catalog.ListLevels();
        Assert.Contains(levels, level => level.Guid == guid);

        var serverStore = new DataStore();
        Assert.True(level.LoadLevel(guid, serverStore));

        CollectCountAction countAction = new();
        serverStore.Query<VoxelEntityDataComponent, CollectCountAction>(0f, ref countAction);
        Assert.True(countAction.Count > 0, "Loading a level should build at least one authority structure.");
    }

    [Fact]
    public void JoinStreamsLevelAndSeatsPlayer()
    {
        ILevelCatalog catalog = _fixture.Catalog;
        LevelSaveService level = new(NullLogger<LevelSaveService>.Instance, catalog, TestBricks.Map);

        Assert.True(catalog.Create("Join Level", seed: "42", GameMode.Creative, out string levelGuid));

        var hub = new ServerConnectionHub();
        var connection = new LocalConnection(new INetworkSerializer[]
        {
            new NsdMessageSerializer<JoinRequest>(),
            new NsdMessageSerializer<JoinAccept>(),
            new NsdMessageSerializer<LevelEntityAdd>(),
            new NsdMessageSerializer<LevelStreamComplete>(),
        });
        hub.Add(connection.Server);

        var serverStore = new DataStore();
        var sessions = new SessionManager();
        ServerJoinSystem join = new(hub, sessions, level, new NetworkReplicationSystem(hub, sessions, NullLogger<NetworkReplicationSystem>.Instance), TestInteractionSystem.Create(hub), NullLogger<ServerJoinSystem>.Instance, TestBricks.Map);

        connection.Client.Send(new JoinRequest
        {
            LevelGuid = levelGuid,
            CharacterId = 7,
            PublicView = new PublicView { CharacterId = 7, Name = "Test", Body = "wb:m_human" },
        });

        join.Tick(delta: 0f, serverStore);

        Result<JoinAccept> accept = connection.Client.Receive<JoinAccept>();
        Assert.True(accept.Success);
        Assert.NotEqual((ulong)0, accept.Value.PlayerEntity);
        Assert.Equal(levelGuid, accept.Value.Level.Guid);

        int streamed = 0;
        Result<LevelEntityAdd> add;
        while ((add = connection.Client.Receive<LevelEntityAdd>()).Success)
        {
            Assert.NotEqual((ulong)0, add.Value.VoxelEntity.Uuid);
            streamed++;
        }

        Result<LevelStreamComplete> complete = connection.Client.Receive<LevelStreamComplete>();
        Assert.True(complete.Success);
        Assert.True(streamed > 0, "Join should stream at least one level entity.");
    }

    [Fact]
    public void JoiningDifferentLevelInLoadedLevelIsRefused()
    {
        ILevelCatalog catalog = _fixture.Catalog;
        LevelSaveService level = new(NullLogger<LevelSaveService>.Instance, catalog, TestBricks.Map);

        Assert.True(catalog.Create("Level A", seed: "111", GameMode.Creative, out string levelA));
        Assert.True(catalog.Create("Level B", seed: "222", GameMode.Creative, out string levelB));

        var hub = new ServerConnectionHub();
        var connection = new LocalConnection(new INetworkSerializer[]
        {
            new NsdMessageSerializer<JoinRequest>(),
            new NsdMessageSerializer<JoinAccept>(),
            new NsdMessageSerializer<LevelEntityAdd>(),
            new NsdMessageSerializer<LevelStreamComplete>(),
            new NsdMessageSerializer<LeaveGameRequest>(),
        });
        hub.Add(connection.Server);

        var serverStore = new DataStore();
        var sessions = new SessionManager();
        ServerJoinSystem join = new(hub, sessions, level, new NetworkReplicationSystem(hub, sessions, NullLogger<NetworkReplicationSystem>.Instance), TestInteractionSystem.Create(hub), NullLogger<ServerJoinSystem>.Instance, TestBricks.Map);

        //  Join level A.
        connection.Client.Send(new JoinRequest { LevelGuid = levelA, CharacterId = 1, PublicView = new PublicView { CharacterId = 1 } });
        join.Tick(0f, serverStore);
        Assert.True(connection.Client.Receive<JoinAccept>().Success);
        while (connection.Client.Receive<LevelEntityAdd>().Success)
        {
        }

        Assert.True(connection.Client.Receive<LevelStreamComplete>().Success);

        //  Leave A (menu exit), then attempt to join the different level B on the same level.
        connection.Client.Send(new LeaveGameRequest { Dummy = 0 });
        join.Tick(0f, serverStore);

        connection.Client.Send(new JoinRequest { LevelGuid = levelB, CharacterId = 1, PublicView = new PublicView { CharacterId = 1 } });
        join.Tick(0f, serverStore);

        //  A level serves exactly one save: the join is refused and the loaded level is unchanged.
        Assert.False(connection.Client.Receive<JoinAccept>().Success);
        Assert.Equal(0, sessions.Count);
        Assert.Equal(levelA, level.CurrentLevelGuid);
    }

    [Fact]
    public void JoiningClientPreservesPreExistingPlayersOnTheSameLevel()
    {
        ILevelCatalog catalog = _fixture.Catalog;
        LevelSaveService level = new(NullLogger<LevelSaveService>.Instance, catalog, TestBricks.Map);

        Assert.True(catalog.Create("Multi Join Level", seed: "99", GameMode.Creative, out string levelGuid));

        INetworkSerializer[] serializers =
        [
            new NsdMessageSerializer<JoinRequest>(),
            new NsdMessageSerializer<JoinAccept>(),
            new NsdMessageSerializer<LevelEntityAdd>(),
            new NsdMessageSerializer<LevelStreamComplete>(),
            new NsdMessageSerializer<WorldSnapshot>(),
            new NsdMessageSerializer<LeaveGameRequest>(),
        ];

        var hub = new ServerConnectionHub();
        var sessions = new SessionManager();
        var serverStore = new DataStore();
        var replication = new NetworkReplicationSystem(hub, sessions, NullLogger<NetworkReplicationSystem>.Instance);
        var join = new ServerJoinSystem(hub, sessions, level, replication, TestInteractionSystem.Create(hub), NullLogger<ServerJoinSystem>.Instance, TestBricks.Map);

        //  The host joins the level first and plays.
        var hostConnection = new LocalConnection(serializers);
        Uuid hostClient = hub.Add(hostConnection.Server);
        hostConnection.Client.Send(new JoinRequest { LevelGuid = levelGuid, CharacterId = 1, PublicView = new PublicView { CharacterId = 1, Name = "Host", Body = "wb:m_human" } });
        join.Tick(0f, serverStore);
        replication.PublishStage(0f, serverStore);

        Assert.True(sessions.TryGetEntity(hostClient, out int hostEntity));
        Uuid hostUuid = serverStore.GetUuid(hostEntity);

        //  A joiner connects to the already-loaded level. Joining must not unload and reload the level,
        //  which would free the host's mirror along with every level entity.
        var guestConnection = new LocalConnection(serializers);
        hub.Add(guestConnection.Server);
        guestConnection.Client.Send(new JoinRequest { LevelGuid = levelGuid, CharacterId = 2, PublicView = new PublicView { CharacterId = 2, Name = "Guest", Body = "wb:m_human" } });
        join.Tick(0f, serverStore);

        Assert.True(sessions.TryGetEntity(hostClient, out int hostAfter));
        Assert.Equal(hostEntity, hostAfter);
        Assert.True(serverStore.TryGet(hostUuid, out _), "The host's mirror must survive a joiner joining the same level.");

        replication.PublishStage(0f, serverStore);

        //  The joiner still completes the handshake and receives the full-state sync.
        Assert.True(guestConnection.Client.Receive<JoinAccept>().Success, "The joiner should receive its join accept.");
        Assert.True(guestConnection.Client.Receive<LevelStreamComplete>().Success, "The joiner should receive the level stream complete.");
        Assert.True(guestConnection.Client.Receive<WorldSnapshot>().Success, "The joiner should receive a full-state snapshot.");
    }

    private struct CollectCountAction : IForEach<VoxelEntityDataComponent>
    {
        public int Count;

        public void Execute(float delta, DataStore store, int entity, in VoxelEntityDataComponent content)
        {
            Count++;
        }
    }

    /// <summary>
    /// A throwaway data root holding real SQLite level databases for the duration of one test.
    /// </summary>
    private sealed class LevelFixture : IDisposable
    {
        private readonly string _dataRoot;

        public ILevelCatalog Catalog { get; }

        public LevelFixture()
        {
            _dataRoot = Path.Combine(Path.GetTempPath(), "wb_levels_" + Guid.NewGuid().ToString("N"));
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
