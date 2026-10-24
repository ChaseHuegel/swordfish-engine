using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using NsdRpc;
using Swordfish.ECS;
using WaywardBeyond.Bricks;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Skills;
using Xunit;
using RpcServerConnection = NsdRpc.IServerConnection;

namespace Swordfish.Tests;

/// <summary>
/// Headless coverage for the authoritative skill grant: XP accrues into the player's transient
/// <see cref="SkillStateComponent"/> from the shipped skill definitions, unknown bricks grant nothing,
/// and seeded totals continue the curve.
/// </summary>
public class ServerSkillSystemTests
{
    [Fact]
    public void BreakingARockGrantsMiningXP()
    {
        Fixture fixture = new();

        fixture.System.OnInteractionApplied(fixture.Store, fixture.Player, fixture.Registry.Id("wb:rock"), isBreak: true);

        Assert.Equal(1, fixture.State.GetXP("mining"));
        Assert.Equal(0, fixture.State.GetXP("building"));
    }

    [Fact]
    public void RepeatedGrantsAccumulateXP()
    {
        Fixture fixture = new();

        fixture.System.OnInteractionApplied(fixture.Store, fixture.Player, fixture.Registry.Id("wb:rock"), isBreak: true);
        fixture.System.OnInteractionApplied(fixture.Store, fixture.Player, fixture.Registry.Id("wb:rock"), isBreak: true);

        Assert.Equal(2, fixture.State.GetXP("mining"));
    }

    [Fact]
    public void PlacingAPanelGrantsBuildingXPButNotMining()
    {
        Fixture fixture = new();

        fixture.System.OnInteractionApplied(fixture.Store, fixture.Player, fixture.Registry.Id("wb:panel"), isBreak: false);

        Assert.Equal(1, fixture.State.GetXP("building"));
        Assert.Equal(0, fixture.State.GetXP("mining"));
    }

    [Fact]
    public void AnUnrecognizedBrickGrantsNoXP()
    {
        Fixture fixture = new();

        fixture.System.OnInteractionApplied(fixture.Store, fixture.Player, brickDataID: 999, isBreak: true);

        Assert.Equal(0, fixture.State.GetXP("mining"));
        Assert.Equal(0, fixture.State.GetXP("salvaging"));
    }

    [Fact]
    public void SeededXPContinuesTheCurve()
    {
        Fixture fixture = new();
        fixture.Store.AddOrUpdate(fixture.Player, new SkillStateComponent(new Dictionary<string, long> { ["mining"] = 61 }));

        fixture.System.OnInteractionApplied(fixture.Store, fixture.Player, fixture.Registry.Id("wb:rock"), isBreak: true);

        Assert.Equal(62, fixture.State.GetXP("mining"));

        Skill mining = fixture.Skills.Get("mining").Value;
        Assert.Equal(new LevelInfo(1, 1), mining.CalculateLevel(fixture.State.GetXP("mining")));
    }

    /// <summary>One world's worth of grant dependencies over the shipped skill definitions.</summary>
    private sealed class Fixture
    {
        public Fixture()
        {
            (Skills, Registry) = SkillDatabaseTests.CreateSharedFixture();

            int player = Store.Alloc(new SkillStateComponent([]), new NetworkComponent());
            Player = player;

            ServerPeer peer = Peers.GetOrAdd(new FakeHost(), new RpcPeer(new FakeServerConnection(clientId: 1)), isLocal: true);
            Sessions.Register(Store, player, peer.GameId, new Session(1));

            System = new ServerSkillSystem(Skills, Sessions, Peers, NullLogger<ServerSkillSystem>.Instance);
        }

        public SkillDatabase Skills { get; }
        public IBrickRegistry Registry { get; }
        public ServerPeers Peers { get; } = new();
        public SessionManager Sessions { get; } = new();
        public DataStore Store { get; } = new();
        public int Player { get; }
        public ServerSkillSystem System { get; }

        public SkillStateComponent State => Store.TryGet(Player, out SkillStateComponent state) ? state : default;
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
