using System;
using System.Reflection;
using System.Threading;
using DryIoc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Util;
using Swordfish.Settings;
using WaywardBeyond.Server.Core;
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
            Host = new ServerWorldHost(container, PendingJoins, settings, NullLoggerFactory.Instance);

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