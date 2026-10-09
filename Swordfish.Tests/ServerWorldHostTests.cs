using System;
using System.Reflection;
using System.Threading;
using DryIoc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Util;
using Swordfish.ECS;
using Swordfish.Settings;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Skills;
using Xunit;

namespace Swordfish.Tests;

public class ServerWorldHostTests
{
    private sealed class Fixture : IDisposable
    {
        public PendingJoins PendingJoins { get; }
        public ServerWorldHost Host { get; }
        public LocalConnection LocalA { get; }
        public LocalConnection LocalB { get; }

        public Fixture()
        {
            NetworkRegistry.Initialize([typeof(InputComponent).Assembly]);

            IContainer container = new Container();
            container.RegisterInstance(new NetworkingSettings());
            container.RegisterInstance(new PhysicsSettings());
            container.RegisterInstance(TestBricks.Map);
            container.RegisterInstance<ILoggerFactory>(NullLoggerFactory.Instance);
            container.RegisterInstance<IInteractionContent>(new StubContent());
            container.RegisterInstance<SkillDatabase>(SkillDatabaseTests.CreateSharedSkillDatabase());
            container.RegisterDelegate<ILogger>(_ => NullLogger.Instance);
            MethodInfo createLogger = typeof(LoggerFactoryExtensions).GetMethod("CreateLogger", [typeof(ILoggerFactory)])!;
            container.Register(typeof(ILogger<>), made: Made.Of(req => createLogger.MakeGenericMethod(req.Parent.ImplementationType)));
            container.RegisterInstance<ILevelCatalog>(new StubLevelCatalog());
            container.RegisterInstance(TestPermissions.EmptyPolicy);
            container.RegisterInstance(new GameplaySettings());

            ServerComposition.Register(container);

            var settings = new NetworkingSettings();
            settings.WorldIdleUnloadMs.Set(50);
            PendingJoins = new PendingJoins();
            var pendingDeletes = new PendingLevelDeletes();
            var levelCatalog = new StubLevelCatalog();
            var levelManager = new ServerLevelManager(PendingJoins, pendingDeletes, levelCatalog, NullLoggerFactory.Instance);
            var hostHeartbeat = new ServerHostHeartbeat(PendingJoins, settings, NullLogger<ServerHostHeartbeat>.Instance);
            Host = new ServerWorldHost(container, levelManager, hostHeartbeat, PendingJoins, pendingDeletes, levelCatalog, settings, NullLoggerFactory.Instance);

            LocalA = new LocalConnection(Serializers);
            LocalB = new LocalConnection(Serializers);
            PendingJoins.Add(LocalA.Server);
            PendingJoins.Add(LocalB.Server);
        }

        public void Dispose()
        {
            Host.Dispose();
        }
    }

    private static readonly INetworkSerializer[] Serializers =
    [
        new NsdMessageSerializer<JoinRequest>(),
        new NsdMessageSerializer<JoinAccept>(),
        new NsdMessageSerializer<LevelEntityAdd>(),
        new NsdMessageSerializer<LevelStreamComplete>(),
        new NsdMessageSerializer<WorldSnapshot>(),
        new NsdMessageSerializer<LeaveGameRequest>(),
        new NsdMessageSerializer<ServerHeartbeatMessage>(),
        new NsdMessageSerializer<NewLevelRequest>(),
        new NsdMessageSerializer<NewLevelResponse>(),
        new NsdMessageSerializer<ListLevelsRequest>(),
        new NsdMessageSerializer<ListLevelsResponse>(),
        new NsdMessageSerializer<DeleteLevelRequest>(),
        new NsdMessageSerializer<DeleteLevelResponse>(),
    ];

    private static readonly MethodInfo _update = typeof(ServerWorldHost)
        .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private sealed class StubContent : IInteractionContent
    {
        public bool TryGetPlaceable(string? itemID, out PlaceableBrick placeable)
        {
            placeable = new PlaceableBrick("wb:panel", WaywardBeyond.Bricks.BrickShape.Block, shapeable: false, hasOrientableTag: false, brightness: 0);
            return true;
        }

        public bool TryGetLoot(ushort brickDataID, out ItemData loot)
        {
            loot = default;
            return false;
        }
    }

    private static void SendJoin(IClientConnection client, string levelGuid, ulong characterId)
    {
        client.Send(new JoinRequest
        {
            LevelGuid = levelGuid,
            CharacterId = characterId,
            PublicView = new PublicView { CharacterId = characterId, Name = "P", Body = "wb:m_human" },
        });
    }

    //  The host ticks on its own thread in production; the test drives the same Update synchronously.
    private static void Pump(ServerWorldHost host)
    {
        try
        {
            _update.Invoke(host, [1f / 64f]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw new InvalidOperationException("Host update failed.", ex.InnerException);
        }
    }

    private static void WaitUntil(ServerWorldHost host, Func<bool> condition, string message, int timeoutMs = 10_000)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        bool result = false;
        while (!(result = condition()) && Environment.TickCount64 < deadline)
        {
            Pump(host);
            Thread.Sleep(10);
        }

        Assert.True(result, message);
    }

    [Fact]
    public void MenuRequestsAreServedToPendingConnections()
    {
        var pendingJoins = new PendingJoins();
        var manager = new ServerLevelManager(pendingJoins, new PendingLevelDeletes(), new StubLevelCatalog(), NullLoggerFactory.Instance);
        var connection = new LocalConnection(Serializers);
        pendingJoins.Add(connection.Server);
        connection.Client.Send(new NewLevelRequest { Name = "T", Seed = "s", GameMode = 0 });

        //  The save backing is unavailable, so creation fails - but the menu still gets its response.
        manager.Tick();
        Result<NewLevelResponse> response = connection.Client.Receive<NewLevelResponse>();
        long deadline = Environment.TickCount64 + 5000;
        while (!response.Success && deadline > Environment.TickCount64)
        {
            Thread.Sleep(10);
            response = connection.Client.Receive<NewLevelResponse>();
        }

        Assert.True(response.Success && !response.Value.Success, "The menu must receive the create-world verdict.");
    }

    [Fact]
    public void InGameSnapshotBurstCarriesTheInventoryEcho()
    {
        using Fixture fixture = new();

        SendJoin(fixture.LocalA.Client, "A", 100);
        Pump(fixture.Host);
        WaitUntil(fixture.Host, () => fixture.Host.PlayerCount == 1 && fixture.LocalA.Client.Receive<JoinAccept>().Success, "Join must complete.");

        //  A long Loading phase: the client builds view entities on a background thread and does not
        //  drain snapshots, while the server keeps publishing. This reproduces the burst the entering-
        //  play coalesce evaluates.
        for (var i = 0; i < 40; i++)
        {
            Pump(fixture.Host);
            Thread.Sleep(1);
        }

        //  Drain the whole burst as the reconcile's entering-play coalesce would evaluate it, tracking
        //  every snapshot that carries an InventoryComponent.
        int inventoryAppearances = 0;
        int snapshotCount = 0;
        int componentCount = 0;
        bool inventoryInNewest = false;
        bool laserSeen = false;
        Result<WorldSnapshot> snapshot;
        while ((snapshot = fixture.LocalA.Client.Receive<WorldSnapshot>()).Success)
        {
            snapshotCount++;
            componentCount += snapshot.Value.Components.Length;
            bool inventoryHere = false;
            foreach (ComponentSnapshot component in snapshot.Value.Components)
            {
                if (NetworkRegistry.TryGetInfo(Uuid.FromValue(component.TypeUuid), out NetworkComponentInfo info)
                    && info.Type == typeof(InventoryComponent))
                {
                    inventoryHere = true;
                    inventoryAppearances++;
                    InventoryComponent inventory = InventoryComponent.Deserialize(component.Payload);
                    if (Array.Exists(inventory.Contents, item => item.ID == "laser" && item.Count > 0))
                    {
                        laserSeen = true;
                    }
                }
            }

            inventoryInNewest = inventoryHere;
        }

        Assert.True(inventoryAppearances > 0, $"The echo burst must carry the inventory at least once. snapshots={snapshotCount} components={componentCount}");
        Assert.True(inventoryInNewest, $"The newest burst snapshot must carry the inventory (the entering-play coalesce keeps only it). appearances={inventoryAppearances}");
        Assert.True(laserSeen, "The starter laser must ride the echo burst.");
    }

    [Fact]
    public void JoinsRouteToTheirWorldAndIdleWorldsUnload()
    {
        using Fixture fixture = new();

        SendJoin(fixture.LocalA.Client, "A", 100);
        SendJoin(fixture.LocalB.Client, "B", 200);
        Pump(fixture.Host);

        //  Each loopback joins its own world; both worlds coexist.
        WaitUntil(
            fixture.Host,
            () => fixture.LocalA.Client.Receive<JoinAccept>().Success && fixture.LocalB.Client.Receive<JoinAccept>().Success,
            "Both clients must be accepted into their own worlds."
        );
        WaitUntil(fixture.Host, () => fixture.Host.PlayerCount == 2, "Both worlds must have one player each.");

        //  B's player leaves: its world unloads after the idle window while A keeps playing.
        fixture.LocalB.Client.Send(new LeaveGameRequest { Dummy = 0 });
        WaitUntil(fixture.Host, () => fixture.Host.PlayerCount == 1, "B's idle world must unload; A must persist.");

        //  A rejoin recreates B's world and resumes.
        SendJoin(fixture.LocalB.Client, "B", 200);
        WaitUntil(fixture.Host, () => fixture.Host.PlayerCount == 2, "A rejoin must recreate B's world.");
    }

    [Fact]
    public void ClientSwitchesWorldsWithoutTearingDownTheOther()
    {
        using Fixture fixture = new();

        //  B joins A's world.
        SendJoin(fixture.LocalA.Client, "A", 100);
        SendJoin(fixture.LocalB.Client, "A", 200);
        WaitUntil(fixture.Host, () => fixture.LocalA.Client.Receive<JoinAccept>().Success, "A must join world A.");
        WaitUntil(fixture.Host, () => fixture.LocalB.Client.Receive<JoinAccept>().Success, "B must join world A.");
        WaitUntil(fixture.Host, () => fixture.Host.PlayerCount == 2, "Both players must share world A.");

        //  B leaves to the menu while A keeps playing in world A.
        fixture.LocalB.Client.Send(new LeaveGameRequest { Dummy = 0 });
        WaitUntil(fixture.Host, () => fixture.Host.PlayerCount == 1, "B leaving must end only B's session.");

        //  B joins a different level. The host must route it to its own world, leaving world A intact.
        SendJoin(fixture.LocalB.Client, "B", 200);
        WaitUntil(fixture.Host, () => fixture.LocalB.Client.Receive<JoinAccept>().Success, "B must be accepted into world B.");
        WaitUntil(fixture.Host, () => fixture.Host.WorldCount == 2, "Joining a different level must create a second world.");
        WaitUntil(fixture.Host, () => fixture.Host.PlayerCount == 2, "A must survive world B's creation.");
    }

    [Fact]
    public void PendingConnectionsReceiveHeartbeatsBeforeJoin()
    {
        using Fixture fixture = new();

        //  Neither loopback sends a JoinRequest, so both stay in the pending set. The host must still
        //  feed their read socket, or a menu-time client's read timeout drops the connection.
        WaitUntil(
            fixture.Host,
            () => fixture.LocalA.Client.Receive<ServerHeartbeatMessage>().Success
                && fixture.LocalB.Client.Receive<ServerHeartbeatMessage>().Success,
            "Pending connections must receive server heartbeats before they join."
        );
    }
}