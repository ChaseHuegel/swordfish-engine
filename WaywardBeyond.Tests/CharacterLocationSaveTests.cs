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
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Permissions;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Skills;
using Xunit;
using RpcServerConnection = NsdRpc.IServerConnection;

namespace WaywardBeyond.Tests;

public class CharacterLocationSaveTests
{
    private const string LEVEL_GUID = "test-level-guid";

    [Fact]
    public async Task LeavePersistsCharacterLocationForRejoinWithoutLevelSave()
    {
        var storage = new InMemoryLevelStorage(LEVEL_GUID);
        using TestHarness harness = CreateHarness(storage);
        ServerPeer peer = harness.AddPeer(clientId: 1, isLocal: false);

        JoinRequest request = CreateJoinRequest(LEVEL_GUID, characterUuid: 42);
        RpcResult<JoinAccept> firstJoin = await harness.JoinAsync(peer, request);
        Assert.True(firstJoin.IsSuccess, firstJoin.ErrorMessage);

        var movedPosition = new Vector3(123.5f, 42.25f, -88.75f);
        Quaternion movedOrientation = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(0.9f, -0.25f, 0.1f));
        harness.MovePlayer(peer.GameId, movedPosition, movedOrientation);

        harness.JoinQueue.EnqueueLeave(peer.GameId);
        harness.Tick();

        RpcResult<JoinAccept> rejoin = await harness.JoinAsync(peer, request);
        Assert.True(rejoin.IsSuccess, rejoin.ErrorMessage);
        AssertTransformEqual(movedPosition, movedOrientation, rejoin.Value);

        harness.SaveService.Dispose();
        Result<CharacterEntityData> persisted = storage.GetCharacterEntity(42);
        Assert.True(persisted.Success, persisted.Message);
        Assert.Equal(movedPosition.X, (float)persisted.Value.X, precision: 4);
        Assert.Equal(movedPosition.Y, (float)persisted.Value.Y, precision: 4);
        Assert.Equal(movedPosition.Z, (float)persisted.Value.Z, precision: 4);
    }

    [Fact]
    public async Task DisconnectPersistsCharacterLocationForRejoinWithoutLevelSave()
    {
        var storage = new InMemoryLevelStorage(LEVEL_GUID);
        using TestHarness harness = CreateHarness(storage);
        ServerPeer firstPeer = harness.AddPeer(clientId: 1, isLocal: false);

        JoinRequest request = CreateJoinRequest(LEVEL_GUID, characterUuid: 77);
        RpcResult<JoinAccept> firstJoin = await harness.JoinAsync(firstPeer, request);
        Assert.True(firstJoin.IsSuccess, firstJoin.ErrorMessage);

        var movedPosition = new Vector3(-310f, 18.5f, 512.75f);
        Quaternion movedOrientation = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(-1.2f, 0.15f, 0f));
        harness.MovePlayer(firstPeer.GameId, movedPosition, movedOrientation);

        harness.JoinQueue.EnqueueDisconnect(firstPeer.GameId);
        harness.Tick();

        ServerPeer reconnectedPeer = harness.AddPeer(clientId: 2, isLocal: false);
        RpcResult<JoinAccept> rejoin = await harness.JoinAsync(reconnectedPeer, request);
        Assert.True(rejoin.IsSuccess, rejoin.ErrorMessage);
        AssertTransformEqual(movedPosition, movedOrientation, rejoin.Value);

        harness.SaveService.Dispose();
        Result<CharacterEntityData> persisted = storage.GetCharacterEntity(77);
        Assert.True(persisted.Success, persisted.Message);
        Assert.Equal(movedPosition.X, (float)persisted.Value.X, precision: 4);
        Assert.Equal(movedPosition.Y, (float)persisted.Value.Y, precision: 4);
        Assert.Equal(movedPosition.Z, (float)persisted.Value.Z, precision: 4);
    }

    [Fact]
    public async Task DirectRejoinPersistsPreviousCharacterLocation()
    {
        var storage = new InMemoryLevelStorage(LEVEL_GUID);
        using TestHarness harness = CreateHarness(storage);
        ServerPeer peer = harness.AddPeer(clientId: 1, isLocal: false);

        JoinRequest request = CreateJoinRequest(LEVEL_GUID, characterUuid: 99);
        RpcResult<JoinAccept> firstJoin = await harness.JoinAsync(peer, request);
        Assert.True(firstJoin.IsSuccess, firstJoin.ErrorMessage);

        var movedPosition = new Vector3(14f, 22f, -35f);
        Quaternion movedOrientation = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(0.4f, 0f, 0f));
        harness.MovePlayer(peer.GameId, movedPosition, movedOrientation);

        RpcResult<JoinAccept> rejoin = await harness.JoinAsync(peer, request);
        Assert.True(rejoin.IsSuccess, rejoin.ErrorMessage);
        AssertTransformEqual(movedPosition, movedOrientation, rejoin.Value);
    }

    [Fact]
    public void TryGetSpawnPointReturnsPendingLocationBeforeDiskWriteCompletes()
    {
        using var writeStarted = new ManualResetEventSlim();
        using var allowWrite = new ManualResetEventSlim();
        var storage = new InMemoryLevelStorage(LEVEL_GUID, onSaveCharacter: _ =>
        {
            writeStarted.Set();
            allowWrite.Wait(TimeSpan.FromSeconds(5));
        });

        using var saveService = CreateSaveService(storage);
        var store = new DataStore();
        Assert.True(saveService.LoadLevel(LEVEL_GUID, store));

        ulong characterUuid = 501;
        int entity = store.Alloc(Uuid.FromValue(characterUuid));
        var position = new Vector3(50f, 12f, -75f);
        Quaternion orientation = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(1.1f, -0.2f, 0f));
        store.AddOrUpdate(entity, new TransformComponent(position, orientation, Vector3.One));

        saveService.QueueSaveCharacterLocation(store, entity);
        Assert.True(writeStarted.Wait(TimeSpan.FromSeconds(5)));

        //  The disk write is still blocked; spawn resolution must immediately return the pending location.
        bool found = saveService.TryGetSpawnPoint(characterUuid, out Vector3 spawnPosition, out Quaternion spawnOrientation);
        Assert.True(found);
        Assert.Equal(position, spawnPosition);
        Assert.Equal(orientation, spawnOrientation);

        allowWrite.Set();
        saveService.Dispose();

        Result<CharacterEntityData> persisted = storage.GetCharacterEntity(characterUuid);
        Assert.True(persisted.Success, persisted.Message);
        Assert.Equal(position.X, (float)persisted.Value.X, precision: 4);
        Assert.Equal(position.Y, (float)persisted.Value.Y, precision: 4);
        Assert.Equal(position.Z, (float)persisted.Value.Z, precision: 4);
    }

    [Fact]
    public void OlderWriteFinishingDoesNotClearNewerPendingLocation()
    {
        int saveIndex = 0;
        using var firstStarted = new ManualResetEventSlim();
        using var allowFirst = new ManualResetEventSlim();
        using var secondStarted = new ManualResetEventSlim();
        using var allowSecond = new ManualResetEventSlim();

        var storage = new InMemoryLevelStorage(LEVEL_GUID, onSaveCharacter: _ =>
        {
            int current = Interlocked.Increment(ref saveIndex);
            if (current == 1)
            {
                firstStarted.Set();
                allowFirst.Wait(TimeSpan.FromSeconds(5));
            }
            else if (current == 2)
            {
                secondStarted.Set();
                allowSecond.Wait(TimeSpan.FromSeconds(5));
            }
        });

        using var saveService = CreateSaveService(storage);
        var store = new DataStore();
        Assert.True(saveService.LoadLevel(LEVEL_GUID, store));

        ulong characterUuid = 502;
        int entity = store.Alloc(Uuid.FromValue(characterUuid));
        var firstPosition = new Vector3(10f, 5f, 20f);
        var secondPosition = new Vector3(90f, 30f, -45f);

        store.AddOrUpdate(entity, new TransformComponent(firstPosition, Quaternion.Identity, Vector3.One));
        saveService.QueueSaveCharacterLocation(store, entity);
        Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(5)));

        //  Queue a newer location while the first write is still blocked.
        store.AddOrUpdate(entity, new TransformComponent(secondPosition, Quaternion.Identity, Vector3.One));
        saveService.QueueSaveCharacterLocation(store, entity);

        //  Let the first write finish while the second write stays blocked.
        allowFirst.Set();
        Assert.True(secondStarted.Wait(TimeSpan.FromSeconds(5)));

        bool found = saveService.TryGetSpawnPoint(characterUuid, out Vector3 spawnPosition, out _);
        Assert.True(found);
        Assert.Equal(secondPosition, spawnPosition);

        allowSecond.Set();
        saveService.Dispose();

        Result<CharacterEntityData> persisted = storage.GetCharacterEntity(characterUuid);
        Assert.True(persisted.Success, persisted.Message);
        Assert.Equal(secondPosition.X, (float)persisted.Value.X, precision: 4);
        Assert.Equal(secondPosition.Y, (float)persisted.Value.Y, precision: 4);
        Assert.Equal(secondPosition.Z, (float)persisted.Value.Z, precision: 4);
    }

    private static void AssertTransformEqual(Vector3 expectedPosition, Quaternion expectedOrientation, in JoinAccept accept)
    {
        Assert.Equal(expectedPosition.X, accept.SpawnX, precision: 4);
        Assert.Equal(expectedPosition.Y, accept.SpawnY, precision: 4);
        Assert.Equal(expectedPosition.Z, accept.SpawnZ, precision: 4);
        Assert.Equal(expectedOrientation.X, accept.OrientationX, precision: 4);
        Assert.Equal(expectedOrientation.Y, accept.OrientationY, precision: 4);
        Assert.Equal(expectedOrientation.Z, accept.OrientationZ, precision: 4);
        Assert.Equal(expectedOrientation.W, accept.OrientationW, precision: 4);
    }

    private static JoinRequest CreateJoinRequest(string levelGuid, ulong characterUuid)
    {
        return new JoinRequest
        {
            LevelGuid = levelGuid,
            CharacterUuid = characterUuid,
            View = new CharacterView
            {
                CharacterUuid = characterUuid,
                Name = "Explorer",
                Body = "default",
            },
            State = new CharacterState
            {
                CharacterUuid = characterUuid,
                Name = "Explorer",
                Body = "default",
                ActiveInventorySlot = 0,
                GameMode = GameMode.Adventure,
            },
        };
    }

    private static LevelSaveService CreateSaveService(InMemoryLevelStorage storage)
    {
        return new LevelSaveService(
            NullLogger.Instance,
            new SingleLevelCatalog(storage),
            BrickRegistry.FromNames(["test"]),
            new FakeBrickDatabase(),
            new GameplayConfig());
    }

    private static TestHarness CreateHarness(InMemoryLevelStorage storage)
    {
        var store = new DataStore();
        var sessions = new SessionManager();
        var saveService = CreateSaveService(storage);
        var peers = new ServerPeers();
        var registry = new NetworkRegistry();
        var replication = new NetworkReplicationSystem(peers, sessions, NullLogger<NetworkReplicationSystem>.Instance, new NetworkingConfig(), registry);
        Swordfish.Physics.IPhysics physics = new FakePhysics();
        var simulationStep = new SimulationStep(store, in physics);
        var joinQueue = new ServerJoinQueue();
        var permissions = new UserPermissionService(new PermissionResolver(PermissionPolicy.Create([])));
        var joinSystem = new ServerJoinSystem(
            sessions,
            saveService,
            replication,
            simulationStep,
            NullLogger<ServerJoinSystem>.Instance,
            new GameplayConfig(),
            new FakeSkillDatabase(),
            joinQueue,
            peers,
            permissions);

        return new TestHarness(store, sessions, saveService, peers, joinQueue, joinSystem);
    }

    private sealed class TestHarness(
        DataStore store,
        SessionManager sessions,
        LevelSaveService saveService,
        ServerPeers peers,
        ServerJoinQueue joinQueue,
        ServerJoinSystem joinSystem
    ) : IDisposable
    {
        private readonly FakeHost _host = new();

        public LevelSaveService SaveService { get; } = saveService;
        public ServerJoinQueue JoinQueue { get; } = joinQueue;

        public ServerPeer AddPeer(ulong clientId, bool isLocal)
        {
            return peers.GetOrAdd(_host, new RpcPeer(new FakeServerConnection(clientId)), isLocal);
        }

        public async Task<RpcResult<JoinAccept>> JoinAsync(ServerPeer peer, JoinRequest request)
        {
            var completion = new TaskCompletionSource<RpcResult<JoinAccept>>(TaskCreationOptions.RunContinuationsAsynchronously);
            joinQueue.Enqueue(peer, request, completion, CancellationToken.None);
            joinSystem.Tick(1f / 60f, store);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void MovePlayer(Uuid clientId, Vector3 position, Quaternion orientation)
        {
            Assert.True(sessions.TryGetEntity(clientId, out int entity));
            store.AddOrUpdate(entity, new TransformComponent(position, orientation, CharacterDefaults.PlayerScale));
        }

        public void Tick()
        {
            joinSystem.Tick(1f / 60f, store);
        }

        public void Dispose()
        {
            saveService.Dispose();
        }
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

    private sealed class InMemoryLevelStorage(
        string levelGuid,
        Action<CharacterEntityData>? onSaveCharacter = null
    ) : ILevelStorage
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<ulong, CharacterEntityData> _characters = [];
        private Level _level = new()
        {
            Guid = levelGuid,
            Name = "Test Level",
            SpawnX = 0,
            SpawnY = 10,
            SpawnZ = 0,
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
            onSaveCharacter?.Invoke(characterEntity);
            lock (_lock)
            {
                _characters[characterEntity.Uuid] = characterEntity;
            }

            return Result.FromSuccess();
        }

        public void Dispose() { }
    }

    private sealed class FakeSkillDatabase : ISkillDatabase
    {
        public Result<Skill> Get(string id) => Result<Skill>.FromFailure((string?)null);

        public IReadOnlyList<Skill> Get(XPSource xpSource) => [];
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

    private sealed class FakeServerConnection(ulong clientId) : RpcServerConnection, IRpcConnectionStats
    {
        public ulong ClientId { get; } = clientId;
        public EndPoint? RemoteEndPoint => null;
        public bool IsConnected => true;
        public RpcConnectionStats Stats => default;

        public void Close(RpcDisconnectReason reason = RpcDisconnectReason.LocalClose) { }

        public bool TrySend(ReadOnlySpan<byte> frame) => true;

        public event Action<RpcDisconnectReason>? Disconnected { add { } remove { } }

        public event RpcFrameHandler? FrameReceived { add { } remove { } }
    }
}
