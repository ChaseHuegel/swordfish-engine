#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using Krill;
using Microsoft.Extensions.Logging;
using NsdRpc;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Config;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Systems;
using Xunit;

namespace WaywardBeyond.Tests;

public class NetworkReplicationStreamGateTests
{
    [Fact]
    public void EndStreamLiftsGateOnIdleTickWithNoDeltas()
    {
        var harness = new Harness();
        ServerPeer peer = harness.AddPeer(1);

        harness.Replication.BeginStream(peer.GameId);
        Assert.Equal(1, harness.Replication.StreamingClientCount);

        harness.Replication.EndStream(peer.GameId);
        harness.Replication.PublishStage(1f, harness.Store);

        Assert.Equal(0, harness.Replication.StreamingClientCount);
        Assert.False(harness.Connections[peer.GameId].Closed);
        Assert.Contains(harness.Logger.Entries, entry =>
            entry.Level == LogLevel.Debug && entry.Message.Contains("queued gate lift", StringComparison.Ordinal));
        Assert.Contains(harness.Logger.Entries, entry =>
            entry.Level == LogLevel.Debug && entry.Message.Contains("Lifted join-stream gate", StringComparison.Ordinal));
    }

    [Fact]
    public void EndStreamLiftsGateOnCadenceSkippedTick()
    {
        var harness = new Harness();
        harness.Config.Protocol.SnapshotHz.Set(10);
        ServerPeer peer = harness.AddPeer(1);

        harness.Replication.BeginStream(peer.GameId);
        harness.Replication.EndStream(peer.GameId);

        //  0.01 s is below the 0.1 s publish interval, so PublishSnapshots returns early.
        harness.Replication.PublishStage(0.01f, harness.Store);

        Assert.Equal(0, harness.Replication.StreamingClientCount);
        Assert.False(harness.Connections[peer.GameId].Closed);
    }

    [Fact]
    public async Task IdleSessionStaysConnectedPastJoinStreamTimeout()
    {
        var harness = new Harness();
        harness.Config.Protocol.JoinStreamTimeoutMs.Set(50);
        ServerPeer peer = harness.AddPeer(1);

        harness.Replication.BeginStream(peer.GameId);
        harness.Replication.RequestFullSync(peer.GameId);
        harness.Replication.PublishStage(1f, harness.Store);
        Assert.Equal(1, harness.Replication.StreamingClientCount);

        //  Complete the stream on a completely idle world with no deltas, removals, or full syncs.
        harness.Replication.EndStream(peer.GameId);
        harness.Replication.PublishStage(1f, harness.Store);
        Assert.Equal(0, harness.Replication.StreamingClientCount);

        await Task.Delay(80);
        harness.Replication.PublishStage(1f, harness.Store);

        Assert.False(harness.Connections[peer.GameId].Closed);
        Assert.DoesNotContain(harness.Logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task StalledStreamWithoutEndStreamClosesPeerAtTimeout()
    {
        var harness = new Harness();
        harness.Config.Protocol.JoinStreamTimeoutMs.Set(30);
        ServerPeer peer = harness.AddPeer(1);

        harness.Replication.BeginStream(peer.GameId);

        await Task.Delay(60);
        harness.Replication.PublishStage(0.01f, harness.Store);

        Assert.Equal(0, harness.Replication.StreamingClientCount);
        Assert.True(harness.Connections[peer.GameId].Closed);
        Assert.Contains(harness.Logger.Entries, entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("EndStream not received", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CompletedStreamPastTimeoutLiftsGateWithoutClosingPeer()
    {
        var harness = new Harness();
        harness.Config.Protocol.JoinStreamTimeoutMs.Set(30);
        ServerPeer peer = harness.AddPeer(1);

        harness.Replication.BeginStream(peer.GameId);
        harness.Replication.EndStream(peer.GameId);

        await Task.Delay(60);
        harness.Replication.PublishStage(0.01f, harness.Store);

        Assert.Equal(0, harness.Replication.StreamingClientCount);
        Assert.False(harness.Connections[peer.GameId].Closed);
        Assert.Contains(harness.Logger.Entries, entry =>
            entry.Level == LogLevel.Warning
            && entry.Message.Contains("lifting the pending gate instead of closing the peer", StringComparison.Ordinal));
    }

    [Fact]
    public void BeginStreamClearsStaleQueuedEndStream()
    {
        var harness = new Harness();
        ServerPeer peer = harness.AddPeer(1);

        harness.Replication.BeginStream(peer.GameId);
        harness.Replication.EndStream(peer.GameId);
        harness.Replication.BeginStream(peer.GameId);

        harness.Replication.PublishStage(1f, harness.Store);

        Assert.Equal(1, harness.Replication.StreamingClientCount);
    }

    [Fact]
    public void EndStreamStillSuppressesDeltasOnCompletionTick()
    {
        var harness = new Harness();
        ServerPeer peer = harness.AddPeer(1);
        int entity = harness.RegisterSession(peer.GameId);

        harness.Replication.BeginStream(peer.GameId);
        harness.Replication.EndStream(peer.GameId);

        //  The completion tick has a dirty server-owned component, but the client stays gated for that publish.
        harness.Store.AddOrUpdate(entity, new TransformComponent(Vector3.UnitX, Quaternion.Identity, Vector3.One));
        harness.Replication.PublishStage(1f, harness.Store);

        Assert.Equal(0, harness.Connections[peer.GameId].SendCount);
        Assert.Equal(0, harness.Replication.StreamingClientCount);

        //  The next publish sends deltas normally because the gate lifted at the end of the completion tick.
        harness.Store.AddOrUpdate(entity, new TransformComponent(Vector3.UnitY, Quaternion.Identity, Vector3.One));
        harness.Replication.PublishStage(1f, harness.Store);

        Assert.Equal(1, harness.Connections[peer.GameId].SendCount);
    }

    private sealed class Harness
    {
        private readonly FakeHost _host = new();
        private readonly ServerPeers _peers = new();
        private readonly SessionManager _sessions = new();

        public DataStore Store { get; } = new();
        public NetworkingConfig Config { get; } = new();
        public RecordingLogger Logger { get; } = new();
        public Dictionary<Uuid, FakeServerConnection> Connections { get; } = [];
        public NetworkReplicationSystem Replication { get; }

        public Harness()
        {
            var registry = new NetworkRegistry();
            registry.Register<TransformComponent>(
                Uuid.FromValue(1),
                NetworkDirection.ServerOwned,
                new TransformCodec());
            registry.Freeze();
            Replication = new NetworkReplicationSystem(_peers, _sessions, Logger, Config, registry);
        }

        public ServerPeer AddPeer(ulong clientId)
        {
            var connection = new FakeServerConnection(clientId);
            ServerPeer peer = _peers.GetOrAdd(_host, new RpcPeer(connection), isLocal: true);
            Connections[peer.GameId] = connection;
            return peer;
        }

        public int RegisterSession(Uuid clientId)
        {
            int entity = Store.Alloc(new NetworkComponent());
            _sessions.Register(Store, entity, clientId, new Session(1));
            return entity;
        }
    }

    private sealed class RecordingLogger : ILogger<NetworkReplicationSystem>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class FakeHost : IRpcHost
    {
        public PeerSet Peers => default;
    }

    private sealed class FakeServerConnection(ulong clientId) : IServerConnection, IRpcConnectionStats
    {
        public ulong ClientId { get; } = clientId;
        public EndPoint? RemoteEndPoint => null;
        public bool IsConnected => !Closed;
        public RpcConnectionStats Stats => default;
        public bool Closed { get; private set; }
        public int SendCount { get; private set; }

        public void Close(RpcDisconnectReason reason = RpcDisconnectReason.LocalClose)
        {
            Closed = true;
        }

        public bool TrySend(ReadOnlySpan<byte> frame)
        {
            SendCount++;
            return true;
        }

        public event Action<RpcDisconnectReason>? Disconnected { add { } remove { } }

        public event RpcFrameHandler? FrameReceived { add { } remove { } }
    }
}
