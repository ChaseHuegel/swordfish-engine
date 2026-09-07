using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
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
/// Headless integration coverage of the server-owned world and the join/full-world-stream path, backed by
/// a real local NATS server (so persistence, not a mock, is exercised). The bundled nats-server binary is
/// copied into the test output by the <c>Server.Core</c> project reference.
/// </summary>
public class ServerJoinStreamTests : IDisposable
{
    private readonly NatsFixture _nats;

    public ServerJoinStreamTests()
    {
        _nats = new NatsFixture();
    }

    public void Dispose()
    {
        _nats.Dispose();
    }

    [Fact]
    public void CreateWorldThenLoadBuildsAuthoritativeBodies()
    {
        using KeyValueStore kv = new(_nats.Configuration);
        WorldSaveService world = new(NullLogger<WorldSaveService>.Instance, () => kv);

        bool created = world.CreateWorld("Test World", seed: "1337", GameMode.Creative, out string guid);
        Assert.True(created);
        Assert.False(string.IsNullOrEmpty(guid));

        Level[] levels = world.ListLevels();
        Assert.Contains(levels, level => level.Guid == guid);

        var serverStore = new DataStore();
        Assert.True(world.LoadLevel(guid, serverStore));

        CollectCountAction countAction = new();
        serverStore.Query<VoxelEntityDataComponent, CollectCountAction>(0f, ref countAction);
        Assert.True(countAction.Count > 0, "Loading a world should build at least one authority structure.");
    }

    [Fact]
    public void JoinStreamsWorldAndSeatsPlayer()
    {
        using KeyValueStore kv = new(_nats.Configuration);
        WorldSaveService world = new(NullLogger<WorldSaveService>.Instance, () => kv);

        Assert.True(world.CreateWorld("Join World", seed: "42", GameMode.Creative, out string levelGuid));

        var hub = new ServerConnectionHub();
        var connection = new LocalConnection(new INetworkSerializer[]
        {
            new NsdMessageSerializer<JoinRequest>(),
            new NsdMessageSerializer<JoinAccept>(),
            new NsdMessageSerializer<WorldEntityAdd>(),
            new NsdMessageSerializer<WorldStreamComplete>(),
        });
        hub.Add(connection.Server);

        var serverStore = new DataStore();
        ServerJoinSystem join = new(hub, new SessionManager(), world, NullLogger<ServerJoinSystem>.Instance);

        connection.Client.Send(new JoinRequest
        {
            LevelGuid = levelGuid,
            CharacterId = 7,
            PublicView = new PublicView { CharacterId = 7, Name = "Test", Body = 3 },
        });

        join.Tick(delta: 0f, serverStore);

        Result<JoinAccept> accept = connection.Client.Receive<JoinAccept>();
        Assert.True(accept.Success);
        Assert.NotEqual((ulong)0, accept.Value.PlayerEntity);
        Assert.Equal(levelGuid, accept.Value.Level.Guid);

        int streamed = 0;
        Result<WorldEntityAdd> add;
        while ((add = connection.Client.Receive<WorldEntityAdd>()).Success)
        {
            Assert.NotEqual((ulong)0, add.Value.VoxelEntity.Uuid);
            streamed++;
        }

        Result<WorldStreamComplete> complete = connection.Client.Receive<WorldStreamComplete>();
        Assert.True(complete.Success);
        Assert.True(streamed > 0, "Join should stream at least one world entity.");
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
    /// Launches the bundled nats-server in JetStream mode against a throwaway store directory and exposes
    /// an <see cref="IConfiguration"/> pointing at it.
    /// </summary>
    private sealed class NatsFixture : IDisposable
    {
        private readonly Process? _process;
        private readonly string _storeDir;

        public IConfiguration Configuration { get; }

        public NatsFixture()
        {
            string binary = Path.Combine(AppContext.BaseDirectory, "assets", "server", "nats", "nats-server");
            _storeDir = Path.Combine(Path.GetTempPath(), "wb_saves_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_storeDir);
            int port = GetFreePort();

            if (!OperatingSystem.IsWindows())
            {
                UnixFileMode mode = File.GetUnixFileMode(binary)
                    | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
                File.SetUnixFileMode(binary, mode);
            }

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = binary,
                    Arguments = $"-js -sd \"{_storeDir}\" -p {port}",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                },
            };

            _ = _process.Start();
            WaitUntilReady(port);
            Configuration = new TestConfiguration($"nats://127.0.0.1:{port}");
        }

        public void Dispose()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit();
                }
            }
            catch
            {
                //  Best-effort teardown.
            }

            try
            {
                if (Directory.Exists(_storeDir))
                {
                    Directory.Delete(_storeDir, recursive: true);
                }
            }
            catch
            {
                //  Best-effort teardown.
            }
        }

        private static void WaitUntilReady(int port)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var client = new TcpClient();
                    client.Connect(IPAddress.Loopback, port);
                    return;
                }
                catch
                {
                    Thread.Sleep(50);
                }
            }

            throw new TimeoutException("Timed out waiting for the test nats-server to accept connections.");
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }

    private sealed class TestConfiguration(string natsUrl) : IConfiguration
    {
        public string? GetString(string key) => key == "NATS_URL" ? natsUrl : null;
        public IPAddress? GetIPAddress(string key) => null;
        public int? GetInt(string key) => null;
    }
}