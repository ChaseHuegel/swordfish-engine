#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using DryIoc;
using Microsoft.Extensions.Logging;
using NsdRpc;
using NsdRpc.Tcp;
using Swordfish.ECS;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Settings;
using Xunit;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Skills;
using WaywardBeyond.Voxels;

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
        Assert.Equal(GameRpcErrors.PERMISSION_DENIED, save.ErrorCode);

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

    [Fact]
    public async Task JoinWithZeroCharacterUuidIsRefused()
    {
        using TestServer server = CreateServer();
        server.WorldHost.Run();
        server.RpcHost.Run();

        var connection = new LoopbackRpcClientConnection(server.Loopback);
        await connection.ConnectAsync();
        var registry = new RpcRegistry();
        registry.Freeze();
        var api = new GameServerApiClient(new RpcClient(connection, registry));

        RpcResult<JoinAccept> join = await api.JoinAsync(CreateJoinRequest(0))
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(join.IsSuccess);
        Assert.Equal(GameRpcErrors.JOIN_REFUSED, join.ErrorCode);

        //  The rejected join must not have unwound the world tick.
        RpcResult<JoinAccept> retry = await api.JoinAsync(CreateJoinRequest(1))
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(retry.IsSuccess, retry.ErrorMessage);

        connection.Close();
    }

    [Fact]
    public async Task JoinWithAnotherPlayersCharacterUuidIsRefused()
    {
        using TestServer server = CreateServer();
        server.WorldHost.Run();
        server.RpcHost.Run();

        var registry = new RpcRegistry();
        registry.Freeze();

        var ownerConnection = new LoopbackRpcClientConnection(server.Loopback);
        await ownerConnection.ConnectAsync();
        var owner = new GameServerApiClient(new RpcClient(ownerConnection, registry));
        RpcResult<JoinAccept> first = await owner.JoinAsync(CreateJoinRequest(42))
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(first.IsSuccess, first.ErrorMessage);

        var otherConnection = new LoopbackRpcClientConnection(server.Loopback);
        await otherConnection.ConnectAsync();
        var other = new GameServerApiClient(new RpcClient(otherConnection, registry));

        RpcResult<JoinAccept> duplicate = await other.JoinAsync(CreateJoinRequest(42))
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(duplicate.IsSuccess);
        Assert.Equal(GameRpcErrors.JOIN_REFUSED, duplicate.ErrorCode);

        //  The refused peer is still seated on its own character, and the world keeps ticking.
        RpcResult<JoinAccept> own = await other.JoinAsync(CreateJoinRequest(43))
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(own.IsSuccess, own.ErrorMessage);

        otherConnection.Close();
        ownerConnection.Close();
    }

    [Fact]
    public async Task JoinStreamGateIsClearedByTheStreamAndByLeave()
    {
        using TestServer server = CreateServer();
        server.WorldHost.Run();
        server.RpcHost.Run();

        var connection = new LoopbackRpcClientConnection(server.Loopback);
        await connection.ConnectAsync();
        var registry = new RpcRegistry();
        registry.Freeze();
        var api = new GameServerApiClient(new RpcClient(connection, registry));

        JoinRequest request = CreateJoinRequest(123);
        RpcResult<JoinAccept> join = await api.JoinAsync(request).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(join.IsSuccess, join.ErrorMessage);

        ServerWorld world = Assert.IsType<ServerWorld>(server.WorldHost.FindWorld(string.Empty));
        Assert.Equal(1, world.Replication.StreamingClientCount);

        var sink = new CollectingSink();
        RpcResult<LevelStreamComplete> stream = await api
            .StreamLevelAsync(new StreamLevelRequest { LevelGuid = string.Empty }, sink)
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(stream.IsSuccess, stream.ErrorMessage);
        Assert.True(
            await WaitForAsync(() => world.Replication.StreamingClientCount == 0),
            "The stream complete must lift the join gate.");

        //  A rejoin opens a new gate; leaving must lift it without a stream ever starting.
        RpcResult<JoinAccept> rejoin = await api.JoinAsync(request).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(rejoin.IsSuccess, rejoin.ErrorMessage);
        Assert.Equal(1, world.Replication.StreamingClientCount);

        api.LeaveGame(new LeaveGameRequest());
        Assert.True(
            await WaitForAsync(() => world.Replication.StreamingClientCount == 0),
            "Leaving must lift the join gate.");

        connection.Close();
    }

    [Fact]
    public async Task WorldResolvesTheComposedInteractionHandlerRegistry()
    {
        using TestServer server = CreateServer();

        //  The registry is composed once in the root. A world that built its own copy would silently
        //  ignore every handler registered before the world existed.
        var handlers = server.Container.Resolve<IInteractionHandlerRegistry>();
        handlers.Register(new RejectingHandler());

        server.WorldHost.Run();
        server.RpcHost.Run();

        var connection = new LoopbackRpcClientConnection(server.Loopback);
        await connection.ConnectAsync();
        var registry = new RpcRegistry();
        registry.Freeze();
        var api = new GameServerApiClient(new RpcClient(connection, registry));

        RpcResult<JoinAccept> join = await api.JoinAsync(CreateJoinRequest(321)).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(join.IsSuccess, join.ErrorMessage);

        ServerWorld world = Assert.IsType<ServerWorld>(server.WorldHost.FindWorld(string.Empty));
        Assert.Same(handlers, world.HandlerRegistry);

        connection.Close();
    }

    [Fact]
    public async Task JoinClampsAnOverlongDisplayName()
    {
        using TestServer server = CreateServer();
        server.WorldHost.Run();
        server.RpcHost.Run();

        var connection = new LoopbackRpcClientConnection(server.Loopback);
        await connection.ConnectAsync();
        var registry = new RpcRegistry();
        registry.Freeze();
        var api = new GameServerApiClient(new RpcClient(connection, registry));

        JoinRequest request = CreateJoinRequest(456, name: new string('a', CharacterNames.MAX_LENGTH * 4));
        RpcResult<JoinAccept> join = await api.JoinAsync(request).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(join.IsSuccess, join.ErrorMessage);

        ServerWorld world = Assert.IsType<ServerWorld>(server.WorldHost.FindWorld(string.Empty));
        Assert.True(world.Store.TryGet(Uuid.FromValue(join.Value.PlayerEntity), out int entity));
        Assert.True(world.Store.TryGet(entity, out IdentifierComponent identifier));
        Assert.Equal(CharacterNames.MAX_LENGTH, identifier.Name?.Length);

        connection.Close();
    }

    private sealed class RejectingHandler : IInteractionHandler
    {
        public InteractionHandlerFilter Filter => default;

        public InteractionResolution Handle(in InteractionContext context) => InteractionResolution.None;
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (!condition() && Environment.TickCount64 < deadline)
        {
            await Task.Delay(10);
        }

        return condition();
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

            if (assembler.Append(
                    groupId: 0,
                    partIndex: (int)chunk.PartIndex,
                    partCount: (int)chunk.PartCount,
                    chunk.Payload,
                    out byte[] payload) == FragmentState.Complete)
            {
                streamed = VoxelEntityData.Deserialize(payload);
            }
        }

        Assert.True(streamed.HasValue);
        Assert.Equal(1234UL, streamed!.Value.Uuid);
        Assert.NotEmpty(streamed.Value.Chunks);
    }

    private static JoinRequest CreateJoinRequest(ulong characterUuid, string name = "Tester")
    {
        return new JoinRequest
        {
            LevelGuid = string.Empty,
            CharacterUuid = characterUuid,
            View = new CharacterView
            {
                CharacterUuid = characterUuid,
                Name = name,
                Body = "default",
            },
            State = new CharacterState
            {
                CharacterUuid = characterUuid,
                Name = name,
                Body = "default",
                ActiveInventorySlot = 0,
                GameMode = GameMode.Adventure,
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
        public IContainer Container { get; } = container;
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
