#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using NsdRpc;
using Swordfish.ECS;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Tests;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Statistics;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Permissions;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Skills;
using Xunit;
using GameState = WaywardBeyond.Client.GameState;
using RpcServerConnection = NsdRpc.IServerConnection;
using WaywardBeyondClient = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

/// <summary>Covers join seeding defaults/bounds, client character persistence, and per-level server survival persistence across leave/rejoin.</summary>
[Collection("ClientGameState")]
public sealed class ServerJoinStatisticsTests
{
    private const string LEVEL_GUID = "stats-level-guid";

    [Theory]
    [InlineData(GameMode.Adventure)]
    [InlineData(GameMode.Creative)]
    public void JoinSeedsAuthoritativeDefaultsForNewCharacters(GameMode mode)
    {
        var store = new DataStore();
        int player = store.Alloc();

        CharacterStatisticsSeeder.SeedAuthoritativeStatistics(store, player, new CharacterState(), mode);

        Assert.True(store.TryGet(player, out StatisticsComponent stats));
        Assert.Equal(StatisticIds.DEFAULT_HEALTH_MAX, stats.GetFixed(StatisticIds.HEALTH));
        Assert.Equal(StatisticIds.DEFAULT_HEALTH_MAX, stats.GetFixed(StatisticIds.HEALTH_MAX));
        Assert.Equal(StatisticIds.DEFAULT_OXYGEN_MAX, stats.GetFixed(StatisticIds.OXYGEN));
        Assert.Equal(StatisticIds.DEFAULT_OXYGEN_MAX, stats.GetFixed(StatisticIds.OXYGEN_MAX));
        Assert.False(stats.GetFlag(StatisticIds.PRESSURIZED));

        foreach (string attrId in StatisticIds.BaseAttributeIds)
        {
            Assert.Equal(StatisticIds.MIN_ATTRIBUTE, stats.GetValue(attrId));
        }
    }

    [Fact]
    public void JoinClampsUntrustedClientSeedAndDropsClientForgedMaxPressureAndBuffs()
    {
        var store = new DataStore();
        int player = store.Alloc();

        var untrustedSeed = new CharacterState
        {
            Statistics =
            [
                new Statistic(StatisticIds.HEALTH, 999_999L),
                new Statistic(StatisticIds.HEALTH_MAX, 999_999L),
                new Statistic(StatisticIds.OXYGEN, -500L),
                new Statistic(StatisticIds.OXYGEN_MAX, 999_999L),
                new Statistic(StatisticIds.PRESSURIZED, 1L),
                new Statistic(StatisticIds.STRENGTH, 99L),
                new Statistic(StatisticIds.PRECISION, -4L),
                new Statistic("wb:buff.godmode.expires_tick", 999_999L),
                new Statistic("mining", 420L),
            ],
        };

        CharacterStatisticsSeeder.SeedAuthoritativeStatistics(store, player, in untrustedSeed, GameMode.Adventure);

        Assert.True(store.TryGet(player, out StatisticsComponent stats));
        Assert.Equal(StatisticIds.DEFAULT_HEALTH_MAX_FIXED, stats.GetValue(StatisticIds.HEALTH_MAX));
        Assert.Equal(StatisticIds.DEFAULT_HEALTH_MAX_FIXED, stats.GetValue(StatisticIds.HEALTH));
        Assert.Equal(StatisticIds.DEFAULT_OXYGEN_MAX_FIXED, stats.GetValue(StatisticIds.OXYGEN_MAX));
        Assert.Equal(0L, stats.GetValue(StatisticIds.OXYGEN));
        Assert.False(stats.GetFlag(StatisticIds.PRESSURIZED));
        Assert.Equal(StatisticIds.MAX_BASE_ATTRIBUTE, stats.GetValue(StatisticIds.STRENGTH));
        Assert.Equal(StatisticIds.MIN_ATTRIBUTE, stats.GetValue(StatisticIds.PRECISION));
        Assert.False(stats.TryGetBuffExpiresTick("godmode", out _));
        Assert.False(stats.TryGetValue("mining", out _));
    }

    [Fact]
    public void JoinPreservesSurvivingClientVitalsInAdventureAndResetsVitalsInCreative()
    {
        var seed = new CharacterState
        {
            Statistics =
            [
                new Statistic(StatisticIds.HEALTH, 62_500L),
                new Statistic(StatisticIds.OXYGEN, 45_000L),
                new Statistic(StatisticIds.AWARENESS, 6L),
            ],
        };

        var store = new DataStore();
        int adventurePlayer = store.Alloc();
        int creativePlayer = store.Alloc();

        CharacterStatisticsSeeder.SeedAuthoritativeStatistics(store, adventurePlayer, in seed, GameMode.Adventure);
        CharacterStatisticsSeeder.SeedAuthoritativeStatistics(store, creativePlayer, in seed, GameMode.Creative);

        Assert.True(store.TryGet(adventurePlayer, out StatisticsComponent advStats));
        Assert.Equal(62.5f, advStats.GetFixed(StatisticIds.HEALTH));
        Assert.Equal(45f, advStats.GetFixed(StatisticIds.OXYGEN));
        Assert.Equal(6L, advStats.GetValue(StatisticIds.AWARENESS));

        Assert.True(store.TryGet(creativePlayer, out StatisticsComponent creativeStats));
        Assert.Equal(StatisticIds.DEFAULT_HEALTH_MAX, creativeStats.GetFixed(StatisticIds.HEALTH));
        Assert.Equal(StatisticIds.DEFAULT_OXYGEN_MAX, creativeStats.GetFixed(StatisticIds.OXYGEN));
        Assert.Equal(6L, creativeStats.GetValue(StatisticIds.AWARENESS));
    }

    [Fact]
    public void ClientCharacterSavePersistsSurvivingStatsWithoutClobberingSkillsOrCounters()
    {
        var character = new Character(
            default,
            77UL,
            _Playtime: new Playtime(),
            "Ada",
            _Body: "wb:human_f",
            _ActiveInventorySlot: 0,
            _GameMode: GameMode.Adventure,
            _Statistics:
            [
                new Statistic(StatisticIds.STRENGTH, 5L),
                new Statistic(StatisticIds.PRECISION, 3L),
                new Statistic(StatisticIds.AWARENESS, 4L),
                new Statistic(StatisticIds.CHARISMA, 2L),
                new Statistic(StatisticIds.EDUCATION, 6L),
                new Statistic(StatisticIds.RESOLVE, 1L),
                new Statistic("mining", 250L),
                new Statistic("bricks.broken", 14L),
                new Statistic("bricks.broken:wb:rock", 25L),
            ],
            _Inventory: null
        );

        var storage = new CapturingCharacterStorage();
        var activeSave = new ActiveCharacterSave { ActiveSave = character };
        var saveManager = new CharacterSaveManager(
            NullLogger<CharacterSaveManager>.Instance,
            storage,
            activeSave,
            new ProfileSettings()
        );

        var store = new DataStore();
        int player = store.Alloc(Uuid.FromValue(character.Uuid));
        store.AddOrUpdate(player, new CharacterComponent(character));
        store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(player, new InventoryComponent());
        store.AddOrUpdate(player, new EquipmentComponent(0));
        store.AddOrUpdate(player, new GameModeComponent(GameMode.Adventure));

        var liveStats = new StatisticsComponent();
        liveStats.SetFixed(StatisticIds.HEALTH, 68.5f);
        liveStats.SetFixed(StatisticIds.HEALTH_MAX, 100f);
        liveStats.SetFixed(StatisticIds.OXYGEN, 40f);
        liveStats.SetFixed(StatisticIds.OXYGEN_MAX, 100f);
        liveStats.SetFlag(StatisticIds.PRESSURIZED, true);
        liveStats.SetBuffExpiresTick("wb:well_fed", 5000L);
        liveStats.SetValue(StatisticIds.STRENGTH, 7L);
        liveStats.SetFixed("mod:shield", 25f);
        liveStats.SetValue("bricks.broken:wb:rock", 14L);
        liveStats.SetValue("bricks.placed:wb:rock", 3L);
        liveStats.SetValue(" :malformed", 9L);
        liveStats.SetValue("wb:", 5L);
        store.AddOrUpdate(player, liveStats);

        GameState previous = WaywardBeyondClient.GameState.Get();
        WaywardBeyondClient.GameState.Set(GameState.Playing);
        try
        {
            saveManager.Save(store);
        }
        finally
        {
            WaywardBeyondClient.GameState.Set(previous);
        }

        Assert.NotNull(storage.Saved);
        Character persisted = storage.Saved!.Value;
        Assert.NotNull(persisted.Statistics);
        Assert.Contains(persisted.Statistics!, s => s.ID == "mining" && s.Value == 250L);
        Assert.Contains(persisted.Statistics!, s => s.ID == "bricks.broken" && s.Value == 14L);
        Assert.Contains(persisted.Statistics!, s => s.ID == "bricks.broken:wb:rock" && s.Value == 25L);
        Assert.Contains(persisted.Statistics!, s => s.ID == StatisticIds.HEALTH && s.Value == 68_500L);
        Assert.Contains(persisted.Statistics!, s => s.ID == StatisticIds.OXYGEN && s.Value == 40_000L);
        Assert.Contains(persisted.Statistics!, s => s.ID == StatisticIds.STRENGTH && s.Value == 7L);
        Assert.Contains(persisted.Statistics!, s => s.ID == StatisticIds.PRECISION && s.Value == 3L);
        Assert.Contains(persisted.Statistics!, s => s.ID == "mod:shield" && s.Value == 25_000L);
        Assert.DoesNotContain(persisted.Statistics!, s => s.ID == "bricks.placed:wb:rock");
        Assert.DoesNotContain(persisted.Statistics!, s => s.ID == " :malformed");
        Assert.DoesNotContain(persisted.Statistics!, s => s.ID == "wb:");
        Assert.DoesNotContain(persisted.Statistics!, s => s.ID == StatisticIds.PRESSURIZED);
        Assert.DoesNotContain(persisted.Statistics!, s => s.ID == StatisticIds.BuffExpiresTick("wb:well_fed"));
    }

    [Fact]
    public async Task ServerLeavePersistsSurvivingStatsAndPrunesExpiredBuffsAcrossRejoin()
    {
        var storage = new InMemoryLevelStorage(LEVEL_GUID);
        using TestHarness harness = CreateHarness(storage);
        ServerPeer peer = harness.AddPeer(clientId: 1);

        var joinRequest = new JoinRequest
        {
            LevelGuid = LEVEL_GUID,
            CharacterUuid = 88UL,
            View = new CharacterView { CharacterUuid = 88UL, Name = "Ada", Body = "wb:human_f" },
            State = new CharacterState
            {
                CharacterUuid = 88UL,
                Name = "Ada",
                Body = "wb:human_f",
                GameMode = GameMode.Adventure,
                Statistics = [new Statistic(StatisticIds.STRENGTH, 5L)],
            },
        };

        RpcResult<JoinAccept> firstJoin = await harness.JoinAsync(peer, joinRequest);
        Assert.True(firstJoin.IsSuccess, firstJoin.ErrorMessage);

        int serverEntity = harness.GetPlayerEntity(peer.GameId);
        harness.AdvanceSimTicks(100);

        Assert.True(harness.Store.SetStatisticFixed(serverEntity, StatisticIds.HEALTH, 42.5f));
        Assert.True(harness.Store.SetStatisticFixed(serverEntity, StatisticIds.HEALTH_MAX, 120f));
        Assert.True(harness.Store.SetStatisticFixed(serverEntity, StatisticIds.OXYGEN, 33f));
        Assert.True(harness.Store.SetStatisticFlag(serverEntity, StatisticIds.PRESSURIZED, true));
        Assert.True(harness.Store.SetBuffExpiresTick(serverEntity, "wb:well_fed", 500L));
        Assert.True(harness.Store.SetBuffExpiresTick(serverEntity, "wb:expired_old", 50L));

        harness.JoinQueue.EnqueueLeave(peer.GameId);
        harness.Tick();

        RpcResult<JoinAccept> rejoin = await harness.JoinAsync(peer, joinRequest);
        Assert.True(rejoin.IsSuccess, rejoin.ErrorMessage);

        int rejoinedEntity = harness.GetPlayerEntity(peer.GameId);
        Assert.True(harness.Store.TryGet(rejoinedEntity, out StatisticsComponent rejoinedStats));
        Assert.Equal(42.5f, rejoinedStats.GetFixed(StatisticIds.HEALTH));
        Assert.Equal(120f, rejoinedStats.GetFixed(StatisticIds.HEALTH_MAX));
        Assert.Equal(33f, rejoinedStats.GetFixed(StatisticIds.OXYGEN));
        Assert.Equal(5L, rejoinedStats.GetValue(StatisticIds.STRENGTH));
        Assert.False(rejoinedStats.GetFlag(StatisticIds.PRESSURIZED));
        Assert.True(rejoinedStats.TryGetBuffExpiresTick("wb:well_fed", out long activeExpiry));
        Assert.Equal(500L, activeExpiry);
        Assert.False(rejoinedStats.TryGetBuffExpiresTick("wb:expired_old", out _));
    }

    [Fact]
    public async Task ServerRestartPreservesRemainingBuffDurationRelativeToNewSimTick()
    {
        var storage = new InMemoryLevelStorage(LEVEL_GUID);
        var joinRequest = new JoinRequest
        {
            LevelGuid = LEVEL_GUID,
            CharacterUuid = 89UL,
            View = new CharacterView { CharacterUuid = 89UL, Name = "Ada", Body = "wb:human_f" },
            State = new CharacterState
            {
                CharacterUuid = 89UL,
                Name = "Ada",
                Body = "wb:human_f",
                GameMode = GameMode.Adventure,
            },
        };

        using (TestHarness firstServer = CreateHarness(storage))
        {
            ServerPeer peer = firstServer.AddPeer(clientId: 1);
            RpcResult<JoinAccept> firstJoin = await firstServer.JoinAsync(peer, joinRequest);
            Assert.True(firstJoin.IsSuccess, firstJoin.ErrorMessage);

            int serverEntity = firstServer.GetPlayerEntity(peer.GameId);
            firstServer.AdvanceSimTicks(10_000);
            Assert.True(firstServer.Store.SetBuffExpiresTick(serverEntity, "wb:well_fed", 10_400L));

            firstServer.JoinQueue.EnqueueLeave(peer.GameId);
            firstServer.Tick();
        }

        //  Simulate a fresh server world restart where CurrentSimTick resets to 0.
        using (TestHarness restartedServer = CreateHarness(storage))
        {
            ServerPeer peer = restartedServer.AddPeer(clientId: 2);
            restartedServer.AdvanceSimTicks(25);
            RpcResult<JoinAccept> rejoin = await restartedServer.JoinAsync(peer, joinRequest);
            Assert.True(rejoin.IsSuccess, rejoin.ErrorMessage);

            int rejoinedEntity = restartedServer.GetPlayerEntity(peer.GameId);
            Assert.True(restartedServer.Store.TryGet(rejoinedEntity, out StatisticsComponent stats));
            Assert.True(stats.TryGetBuffExpiresTick("wb:well_fed", out long restoredExpiry));
            Assert.Equal(425L, restoredExpiry);
        }
    }

    private static TestHarness CreateHarness(InMemoryLevelStorage storage)
    {
        var store = new DataStore();
        var sessions = new SessionManager();
        var physics = new SteppingFakePhysics();
        var simulationStep = new SimulationStep(store, physics);
        var saveService = new LevelSaveService(
            NullLogger.Instance,
            new SingleLevelCatalog(storage),
            BrickRegistry.FromNames(["test"]),
            new FakeBrickDatabase(),
            new GameplayConfig());
        var peers = new ServerPeers();
        var registry = new NetworkRegistry();
        var replication = new NetworkReplicationSystem(peers, sessions, NullLogger<NetworkReplicationSystem>.Instance, new NetworkingConfig(), registry);
        var joinQueue = new ServerJoinQueue();
        var permissions = new UserPermissionService(new PermissionResolver(PermissionPolicy.Create([])));
        var joinSystem = new ServerJoinSystem(
            sessions,
            saveService,
            replication,
            simulationStep,
            NullLogger<ServerJoinSystem>.Instance,
            new GameplayConfig(),
            new StubSkillDatabase(),
            joinQueue,
            peers,
            permissions);

        return new TestHarness(store, sessions, saveService, peers, joinQueue, joinSystem, physics);
    }

    private sealed class TestHarness(
        DataStore store,
        SessionManager sessions,
        LevelSaveService saveService,
        ServerPeers peers,
        ServerJoinQueue joinQueue,
        ServerJoinSystem joinSystem,
        SteppingFakePhysics physics
    ) : IDisposable
    {
        private readonly FakeHost _host = new();

        public DataStore Store { get; } = store;
        public ServerJoinQueue JoinQueue { get; } = joinQueue;

        public ServerPeer AddPeer(ulong clientId)
        {
            return peers.GetOrAdd(_host, new RpcPeer(new FakeServerConnection(clientId)), isLocal: false);
        }

        public async Task<RpcResult<JoinAccept>> JoinAsync(ServerPeer peer, JoinRequest request)
        {
            var completion = new TaskCompletionSource<RpcResult<JoinAccept>>(TaskCreationOptions.RunContinuationsAsynchronously);
            JoinQueue.Enqueue(peer, request, completion, CancellationToken.None);
            joinSystem.Tick(1f / 60f, Store);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public int GetPlayerEntity(Uuid clientId)
        {
            Assert.True(sessions.TryGetEntity(clientId, out int entity));
            return entity;
        }

        public void AdvanceSimTicks(int count)
        {
            for (var i = 0; i < count; i++)
            {
                physics.Step();
            }
        }

        public void Tick()
        {
            joinSystem.Tick(1f / 60f, Store);
        }

        public void Dispose()
        {
            saveService.Dispose();
        }
    }

    private sealed class SteppingFakePhysics : IServerWorldPhysics
    {
        public float FixedTimestamp => 1f / 64f;

        public event EventHandler<EventArgs>? FixedUpdate;

        public RaycastResult Raycast(in Ray ray) => default;

        public bool Overlaps(in ShapeOverlapQuery query) => false;

        public void SetGravity(Vector3 gravity) { }

        public void Tick(float delta, DataStore store) { }

        public void Step()
        {
            FixedUpdate?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class CapturingCharacterStorage : ICharacterStorage
    {
        public Character? Saved { get; private set; }

        public Result<Character> GetCharacter(ulong id) => Result<Character>.FromFailure("Not implemented");

        public IReadOnlyList<Character> GetAllCharacters() => Saved != null ? [Saved.Value] : [];

        public Result SaveCharacter(Character character)
        {
            Saved = character;
            return Result.FromSuccess();
        }

        public Result DeleteCharacter(ulong id) => Result.FromSuccess();
    }

    private sealed class SingleLevelCatalog(InMemoryLevelStorage storage) : ILevelCatalog
    {
        public bool Create(string name, string seed, GameMode gameMode, out string levelGuid)
        {
            levelGuid = storage.LevelGuid;
            return true;
        }

        public Level[] ListLevels() => [];

        public bool Delete(string levelGuid) => true;

        public bool Exists(string levelGuid) => levelGuid == storage.LevelGuid;

        public ILevelStorage? Open(string levelGuid) => levelGuid == storage.LevelGuid ? storage : null;
    }

    private sealed class InMemoryLevelStorage(string levelGuid) : ILevelStorage
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<ulong, CharacterEntityData> _characters = [];
        private Level _level = new()
        {
            Guid = levelGuid,
            Name = "Stats Test Level",
            SpawnX = 0,
            SpawnY = 10,
            SpawnZ = 0,
            DefaultGameMode = GameMode.Adventure,
        };

        public string LevelGuid { get; } = levelGuid;

        public Result<Level> GetLevel()
        {
            lock (_lock)
            {
                return Result<Level>.FromSuccess(_level);
            }
        }

        public IReadOnlyList<VoxelEntityData> GetVoxelEntities(uint dataVersion) => [];

        public Result<CharacterEntityData> GetCharacterEntity(ulong uuid)
        {
            lock (_lock)
            {
                return _characters.TryGetValue(uuid, out CharacterEntityData data)
                    ? Result<CharacterEntityData>.FromSuccess(data)
                    : Result<CharacterEntityData>.FromFailure("Location not found.");
            }
        }

        public Result Save(Level level, IReadOnlyList<VoxelEntityData> voxelEntities, IReadOnlyList<CharacterEntityData> characterEntities)
        {
            lock (_lock)
            {
                _level = level;
                foreach (CharacterEntityData characterEntity in characterEntities)
                {
                    _characters[characterEntity.Uuid] = characterEntity;
                }
            }

            return Result.FromSuccess();
        }

        public Result Save(Level level)
        {
            lock (_lock)
            {
                _level = level;
            }

            return Result.FromSuccess();
        }

        public Result Save(CharacterEntityData characterEntity)
        {
            lock (_lock)
            {
                _characters[characterEntity.Uuid] = characterEntity;
            }

            return Result.FromSuccess();
        }

        public void Dispose() { }
    }

    private sealed class FakeBrickDatabase : IBrickDatabase
    {
        public Result<Brick> Get(string id) => Result<Brick>.FromFailure((string?)null);

        public Result<Brick> Get(ushort id) => Result<Brick>.FromFailure((string?)null);

        public List<Brick> Get(Func<Brick, bool> predicate) => [];

        public bool IsCuller(in Voxel voxel, BrickShape shape) => false;
    }

    private sealed class FakeHost : IRpcHost
    {
        public PeerSet Peers => default;
    }

    private sealed class FakeServerConnection(ulong clientId) : RpcServerConnection
    {
        public ulong ClientId { get; } = clientId;
        public EndPoint? RemoteEndPoint => null;
        public bool IsConnected => true;

        public void Close(RpcDisconnectReason reason = RpcDisconnectReason.LocalClose) { }

        public bool TrySend(ReadOnlySpan<byte> frame) => true;

        public event Action<RpcDisconnectReason>? Disconnected { add { } remove { } }

        public event RpcFrameHandler? FrameReceived { add { } remove { } }
    }
}
