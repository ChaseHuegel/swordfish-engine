#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using DryIoc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NsdRpc;
using NsdRpc.Tcp;
using Swordfish.ECS;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Settings;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Voxels;
using WaywardBeyond.Skills;
using Xunit;

namespace Swordfish.Tests;

public class ServerWorldIntegrationTests
{
    [Fact]
    public async Task LoopbackJoinStreamSaveLeaveAndRejoin()
    {
        using TestServer server = CreateServer();
        server.WorldHost.Run();
        server.RpcHost.Run();

        var connection = new LoopbackRpcClientConnection(server.Loopback);
        await connection.ConnectAsync();
        var registry = new RpcRegistry();
        registry.Freeze();
        var api = new GameServerApiClient(new RpcClient(connection, registry));

        JoinRequest request = CreateJoinRequest(42);
        RpcResult<JoinAccept> join = await api.JoinAsync(request).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(join.IsSuccess, join.ErrorMessage);
        Assert.NotEqual(0UL, join.Value.PlayerEntity);

        ServerWorld world = Assert.IsType<ServerWorld>(server.WorldHost.FindWorld(string.Empty));
        SeedVoxelEntity(world);

        var sink = new CollectingSink();
        RpcResult<LevelStreamComplete> stream = await api
            .StreamLevelAsync(new StreamLevelRequest { LevelGuid = string.Empty }, sink)
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(stream.IsSuccess, stream.ErrorMessage);
        Assert.NotEmpty(sink.Chunks);
        AssertStreamedVoxelEntity(sink.Chunks);

        RpcResult<SaveLevelResponse> save = await api
            .SaveLevelAsync(new SaveLevelRequest())
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(save.IsSuccess, save.ErrorMessage);
        Assert.True(save.Value.Success);

        api.LeaveGame(new LeaveGameRequest());
        RpcResult<JoinAccept> rejoin = await api.JoinAsync(request).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(rejoin.IsSuccess, rejoin.ErrorMessage);

        connection.Close();
    }

    [Fact]
    public async Task TcpJoinStreamSaveLeaveAndRejoin()
    {
        using TestServer server = CreateServer();
        server.WorldHost.Run();
        server.RpcHost.Run();
        server.RpcHost.StartLan(0);

        var connection = new TcpClientConnection(
            new RpcOptions(),
            new TcpOptions { Host = "127.0.0.1", Port = server.RpcHost.LanPort });
        await connection.ConnectAsync();
        var registry = new RpcRegistry();
        registry.Freeze();
        var api = new GameServerApiClient(new RpcClient(connection, registry));

        JoinRequest request = CreateJoinRequest(77);
        RpcResult<JoinAccept> join = await api.JoinAsync(request).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(join.IsSuccess, join.ErrorMessage);
        Assert.NotEqual(0UL, join.Value.PlayerEntity);

        var sink = new CollectingSink();
        RpcResult<LevelStreamComplete> stream = await api
            .StreamLevelAsync(new StreamLevelRequest { LevelGuid = string.Empty }, sink)
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(stream.IsSuccess, stream.ErrorMessage);

        RpcResult<SaveLevelResponse> save = await api
            .SaveLevelAsync(new SaveLevelRequest())
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(save.IsSuccess);
        Assert.Equal(GameRpcErrors.PermissionDenied, save.ErrorCode);

        api.LeaveGame(new LeaveGameRequest());
        RpcResult<JoinAccept> rejoin = await api.JoinAsync(request).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(rejoin.IsSuccess, rejoin.ErrorMessage);

        connection.Close();
    }

    [Fact]
    public async Task MidJoinDisconnectFailsTheCallInsteadOfHanging()
    {
        using TestServer server = CreateServer();
        server.WorldHost.Run();
        server.RpcHost.Run();

        var connection = new LoopbackRpcClientConnection(server.Loopback);
        await connection.ConnectAsync();
        var registry = new RpcRegistry();
        registry.Freeze();
        var api = new GameServerApiClient(new RpcClient(connection, registry));

        Task<RpcResult<JoinAccept>> join = api.JoinAsync(CreateJoinRequest(99));
        connection.Close();

        RpcResult<JoinAccept> result = await join.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.IsSuccess);
    }

    private static void SeedVoxelEntity(ServerWorld world)
    {
        var voxelObject = new VoxelObject(chunkSize: 16);
        voxelObject.Set(0, 0, 0, new Voxel(1, 0, 0));

        int entity = world.Store.Alloc(Uuid.FromValue(1234));
        world.Store.AddOrUpdate(entity, new NetworkComponent());
        world.Store.AddOrUpdate(entity, new TransformComponent());
        world.Store.AddOrUpdate(entity, new VoxelWorldComponent(voxelObject));
    }

    private static void AssertStreamedVoxelEntity(List<LevelStreamChunk> chunks)
    {
        var assembler = new FragmentAssembler();
        VoxelEntityData? streamed = null;
        for (var index = 0; index < chunks.Count; index++)
        {
            LevelStreamChunk chunk = chunks[index];
            Assert.Equal((uint)index, chunk.PartIndex);

            if (assembler.TryAppend(
                    groupId: 0,
                    partIndex: (int)chunk.PartIndex,
                    partCount: (int)chunk.PartCount,
                    chunk.Payload,
                    out byte[] payload))
            {
                streamed = VoxelEntityData.Deserialize(payload);
            }
        }

        Assert.True(streamed.HasValue);
        Assert.Equal(1234UL, streamed!.Value.Uuid);
        Assert.NotEmpty(streamed.Value.Chunks);
    }

    private static JoinRequest CreateJoinRequest(ulong characterUuid)
    {
        return new JoinRequest
        {
            LevelGuid = string.Empty,
            CharacterUuid = characterUuid,
            View = new CharacterView
            {
                CharacterUuid = characterUuid,
                Name = "Tester",
                Body = "default",
            },
            State = new CharacterState
            {
                CharacterUuid = characterUuid,
                Name = "Tester",
                Body = "default",
                ActiveInventorySlot = 0,
                GameMode = 1,
            },
        };
    }

    private static TestServer CreateServer()
    {
        var container = new Container();
        var catalog = new FakeLevelCatalog();
        var config = new NetworkingConfig();

        container.RegisterInstance<IContainer>(container);
        container.RegisterInstance<ILoggerFactory>(new TestLoggerFactory());
        container.RegisterInstance<ILogger>(new TestLogger("Server"));
        RegisterTestLoggers(container, typeof(ServerWorldHost).Assembly);
        container.RegisterInstance(config);
        container.RegisterInstance(new GameplayConfig());
        container.RegisterInstance(new LevelGeneratorConfig());
        container.RegisterInstance(new PhysicsSettings());
        container.RegisterInstance(new StorageConfig());
        container.RegisterDelegate<ILevelCatalog>(_ => catalog, Reuse.Singleton);
        container.RegisterDelegate<IBrickRegistry>(_ => BrickRegistry.FromNames(["test"]), Reuse.Singleton);
        container.RegisterDelegate<ISkillDatabase>(_ => new FakeSkillDatabase(), Reuse.Singleton);
        container.RegisterDelegate<IInteractionContent>(_ => new FakeInteractionContent(), Reuse.Singleton);
        container.RegisterDelegate<PermissionPolicy>(_ => PermissionPolicy.Create([]), Reuse.Singleton);
        new ServerModule().Inject(container);

        return new TestServer(
            container,
            container.Resolve<ServerWorldHost>(),
            container.Resolve<ServerRpcHost>(),
            container.Resolve<LoopbackRpcServerTransport>());
    }

    private static void RegisterTestLoggers(IContainer container, System.Reflection.Assembly assembly)
    {
        foreach (Type type in assembly.GetTypes())
        {
            if (!type.IsClass || type.ContainsGenericParameters)
            {
                continue;
            }

            Type serviceType = typeof(ILogger<>).MakeGenericType(type);
            Type loggerType = typeof(TestLogger<>).MakeGenericType(type);
            container.RegisterInstance(serviceType, Activator.CreateInstance(loggerType)!);
        }
    }

    private sealed class TestLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new TestLogger(categoryName);

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }
    }

    private class TestLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) {
            Console.WriteLine($"[{logLevel}] {category}: {formatter(state, exception)}");
            if (exception != null)
            {
                Console.WriteLine(exception);
            }
        }
    }

    private sealed class TestLogger<T> : TestLogger, ILogger<T>
    {
        public TestLogger() : base(typeof(T).Name) { }
    }

    private sealed class TestServer(
        IContainer container,
        ServerWorldHost worldHost,
        ServerRpcHost rpcHost,
        LoopbackRpcServerTransport loopback
    ) : IDisposable
    {
        public ServerWorldHost WorldHost { get; } = worldHost;
        public ServerRpcHost RpcHost { get; } = rpcHost;
        public LoopbackRpcServerTransport Loopback { get; } = loopback;

        public void Dispose()
        {
            WorldHost.Dispose();
            RpcHost.Dispose();
            container.Dispose();
        }
    }

    private sealed class CollectingSink : IChunkSink<LevelStreamChunk>
    {
        public List<LevelStreamChunk> Chunks { get; } = [];

        public void OnChunk(in LevelStreamChunk chunk)
        {
            Chunks.Add(chunk);
        }
    }

    private sealed class FakePhysics : IServerWorldPhysics
    {
        public float FixedTimestamp => 1f / 64f;

        public event EventHandler<EventArgs>? FixedUpdate;

        public RaycastResult Raycast(in Ray ray) => default;

        public void SetGravity(Vector3 gravity) { }

        public void Tick(float delta, DataStore store)
        {
            FixedUpdate?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeSkillDatabase : ISkillDatabase
    {
        public Result<Skill> Get(string id) => Result<Skill>.FromFailure((string?)null);

        public IReadOnlyList<Skill> Get(XPSource xpSource) => [];
    }

    private sealed class FakeInteractionContent : IInteractionContent
    {
        public Result<Brick> GetPlaceable(string? itemID) => Result<Brick>.FromFailure((string?)null);

        public Result<ItemData> GetLoot(ushort brickDataID) => Result<ItemData>.FromFailure((string?)null);
    }

    private sealed class FakeLevelCatalog : ILevelCatalog
    {
        public bool Create(string name, string seed, GameMode gameMode, out string levelGuid)
        {
            levelGuid = Guid.NewGuid().ToString();
            return true;
        }

        public Level[] ListLevels() => [];

        public bool Delete(string levelGuid) => true;

        public bool Exists(string levelGuid) => false;

        public ILevelStorage? Open(string levelGuid) => null;
    }
}
