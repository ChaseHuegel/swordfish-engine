using System;
using System.Numerics;
using DryIoc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using Swordfish.Settings;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Skills;
using Xunit;

namespace Swordfish.Tests;

public class ClientInventoryEchoTests
{
    private sealed class Fixture : IDisposable
    {
        public PendingJoins PendingJoins { get; }
        public ServerWorldHost Host { get; }
        public LocalConnection Local { get; }

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
            System.Reflection.MethodInfo createLogger = typeof(LoggerFactoryExtensions).GetMethod("CreateLogger", [typeof(ILoggerFactory)])!;
            container.Register(typeof(ILogger<>), made: Made.Of(req => createLogger.MakeGenericMethod(req.Parent.ImplementationType)));
            container.RegisterInstance<ILevelCatalog>(new StubLevelCatalog());
            container.RegisterInstance(TestPermissions.EmptyPolicy);
            container.RegisterInstance(new GameplaySettings());

            ServerComposition.Register(container);

            var settings = new NetworkingSettings();
            PendingJoins = new PendingJoins();
            var pendingDeletes = new PendingLevelDeletes();
            var levelCatalog = new StubLevelCatalog();
            var levelManager = new ServerLevelManager(PendingJoins, pendingDeletes, levelCatalog, TestPermissions.EmptyPolicy, new ConnectionClaims(), NullLoggerFactory.Instance);
            var hostHeartbeat = new ServerHostHeartbeat(PendingJoins, settings, NullLogger<ServerHostHeartbeat>.Instance);
            Host = new ServerWorldHost(container, levelManager, hostHeartbeat, PendingJoins, pendingDeletes, levelCatalog, settings, NullLoggerFactory.Instance);

            Local = new LocalConnection(Serializers);
            PendingJoins.Add(Local.Server);
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
    ];

    private static readonly System.Reflection.MethodInfo _update = typeof(ServerWorldHost)
        .GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

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

    [Fact]
    public void ClientPlayerInventoryFillsFromTheEcho()
    {
        using Fixture fixture = new();
        LocalConnection local = fixture.Local;

        local.Client.Send(new JoinRequest
        {
            CharacterId = 100,
            PublicView = new PublicView { CharacterId = 100, Name = "P", Body = "wb:m_human" },
        });
        _update.Invoke(fixture.Host, [1f / 64f]);

        //  The client seats its player (mirroring PlayerCharacterEntityBuilder: PendingInventory +
        //  empty inventory for a fresh character) and then applies the echo as entering-play does:
        //  the burst coalesces to the newest snapshot, which is applied wholesale.
        long deadline = Environment.TickCount64 + 5000;
        Result<JoinAccept> accept = local.Client.Receive<JoinAccept>();
        while (!accept.Success && deadline > Environment.TickCount64)
        {
            _update.Invoke(fixture.Host, [1f / 64f]);
            System.Threading.Thread.Sleep(5);
            accept = local.Client.Receive<JoinAccept>();
        }

        Assert.True(accept.Success, "Join must complete.");
        var store = new DataStore();
        int player = store.Alloc(Uuid.FromValue(accept.Value.PlayerEntity));
        store.AddOrUpdate(player, new InventoryComponent());

        //  Publish rides the SnapshotHz cadence, so keep ticking until the full-sync snapshot lands.
        WorldSnapshot? newest = null;
        long snapshotDeadline = Environment.TickCount64 + 5000;
        while (newest == null && snapshotDeadline > Environment.TickCount64)
        {
            _update.Invoke(fixture.Host, [1f / 64f]);

            Result<WorldSnapshot> snapshot;
            while ((snapshot = local.Client.Receive<WorldSnapshot>()).Success)
            {
                newest = snapshot.Value;
            }

            if (newest == null)
            {
                System.Threading.Thread.Sleep(5);
            }
        }

        Assert.NotNull(newest);
        foreach (ComponentSnapshot component in newest.Value.Components)
        {
            if (NetworkRegistry.TryGetInfo(Uuid.FromValue(component.TypeUuid), out NetworkComponentInfo info)
                && info.Direction == NetworkDirection.ServerOwned)
            {
                info.Codec.Apply(store, player, component.Payload);
            }
        }

        Assert.True(store.TryGet(player, out InventoryComponent inventory));
        Assert.Contains(inventory.Contents, item => item.ID == "laser" && item.Count > 0);
    }
}