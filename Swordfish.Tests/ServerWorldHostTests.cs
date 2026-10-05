using System;
using System.Reflection;
using System.Threading;
using DryIoc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Util;
using Swordfish.ECS;
using Swordfish.Settings;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;
using WaywardBeyond.Shared.Skills;
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
            container.RegisterDelegate<Func<KeyValueStore>>(_ => () => throw new NotImplementedException());

            ServerComposition.Register(container);

            var settings = new NetworkingSettings();
            settings.WorldIdleUnloadMs.Set(50);
            PendingJoins = new PendingJoins();
            var worldManager = new ServerWorldManager(PendingJoins, () => throw new NotImplementedException(), TestBricks.Map, NullLoggerFactory.Instance);
            Host = new ServerWorldHost(container, worldManager, PendingJoins, settings, NullLoggerFactory.Instance);

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
        new NsdMessageSerializer<WorldEntityAdd>(),
        new NsdMessageSerializer<WorldStreamComplete>(),
        new NsdMessageSerializer<WorldSnapshot>(),
        new NsdMessageSerializer<LeaveGameRequest>(),
        new NsdMessageSerializer<ServerHeartbeatMessage>(),
        new NsdMessageSerializer<NewWorldRequest>(),
        new NsdMessageSerializer<NewWorldResponse>(),
        new NsdMessageSerializer<ListWorldsRequest>(),
        new NsdMessageSerializer<ListWorldsResponse>(),
        new NsdMessageSerializer<DeleteWorldRequest>(),
        new NsdMessageSerializer<DeleteWorldResponse>(),
    ];

    private static readonly MethodInfo _update = typeof(ServerWorldHost)
        .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private sealed class StubContent : IInteractionContent
    {
        public bool TryGetPlaceable(string? itemID, out PlaceableBrick placeable)
        {
            placeable = new PlaceableBrick("wb:panel", WaywardBeyond.Shared.Bricks.BrickShape.Block, shapeable: false, hasOrientableTag: false, brightness: 0);
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
        var manager = new ServerWorldManager(pendingJoins, () => throw new NotSupportedException(), TestBricks.Map, NullLoggerFactory.Instance);
        var connection = new LocalConnection(Serializers);
        pendingJoins.Add(connection.Server);
        connection.Client.Send(new NewWorldRequest { Name = "T", Seed = "s", GameMode = 0 });

        //  The save backing is unavailable, so creation fails - but the menu still gets its response.
        manager.Tick();
        Result<NewWorldResponse> response = connection.Client.Receive<NewWorldResponse>();
        long deadline = Environment.TickCount64 + 5000;
        while (!response.Success && deadline > Environment.TickCount64)
        {
            Thread.Sleep(10);
            response = connection.Client.Receive<NewWorldResponse>();
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
}