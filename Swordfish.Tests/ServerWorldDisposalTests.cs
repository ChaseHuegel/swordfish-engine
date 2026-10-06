using System;
using System.Reflection;
using System.Threading;
using DryIoc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using Swordfish.Settings;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;
using WaywardBeyond.Shared.Skills;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// A world's child container must never dispose the root's singletons when the world unloads or the
/// host shuts down: DryIoc's CreateChild disposal cascades to root singleton disposables unless
/// <c>withDisposables: false</c> is used. A root singleton is disposed exactly once, by the root.
/// </summary>
public class ServerWorldDisposalTests : IDisposable
{
    private static readonly INetworkSerializer[] Serializers =
    [
        new NsdMessageSerializer<JoinRequest>(),
        new NsdMessageSerializer<JoinAccept>(),
        new NsdMessageSerializer<LevelEntityAdd>(),
        new NsdMessageSerializer<LevelStreamComplete>(),
        new NsdMessageSerializer<WorldSnapshot>(),
    ];

    private static readonly MethodInfo _update = typeof(ServerWorldHost)
        .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly IContainer _container;
    private readonly PendingJoins _pendingJoins;
    private readonly ServerWorldHost _host;

    private sealed class RootDisposable : IDisposable
    {
        public int Disposals { get; private set; }
        public void Dispose() => Disposals++;
    }

    public ServerWorldDisposalTests()
    {
        NetworkRegistry.Initialize([typeof(InputComponent).Assembly]);

        _container = new Container();
        _container.RegisterInstance(new NetworkingSettings());
        _container.RegisterInstance(new PhysicsSettings());
        _container.RegisterInstance(TestBricks.Map);
        _container.RegisterInstance<ILoggerFactory>(NullLoggerFactory.Instance);
        _container.RegisterInstance<IInteractionContent>(new StubContent());
        _container.RegisterInstance<SkillDatabase>(SkillDatabaseTests.CreateSharedSkillDatabase());
        _container.RegisterDelegate<ILogger>(_ => NullLogger.Instance);
        MethodInfo createLogger = typeof(LoggerFactoryExtensions).GetMethod("CreateLogger", [typeof(ILoggerFactory)])!;
        _container.Register(typeof(ILogger<>), made: Made.Of(req => createLogger.MakeGenericMethod(req.Parent.ImplementationType)));
        _container.RegisterInstance<ILevelCatalog>(new StubLevelCatalog());
        _container.RegisterInstance(TestPermissions.EmptyPolicy);
        _container.RegisterInstance(new GameplaySettings());
        _container.Register<RootDisposable>(Reuse.Singleton);

        ServerComposition.Register(_container);

        _pendingJoins = new PendingJoins();
        var pendingDeletes = new PendingLevelDeletes();
        var levelCatalog = new StubLevelCatalog();
        var settings = new NetworkingSettings();
        var levelManager = new ServerLevelManager(_pendingJoins, pendingDeletes, levelCatalog, NullLoggerFactory.Instance);
        var hostHeartbeat = new ServerHostHeartbeat(_pendingJoins, settings, NullLogger<ServerHostHeartbeat>.Instance);
        _host = new ServerWorldHost(_container, levelManager, hostHeartbeat, _pendingJoins, pendingDeletes, levelCatalog, settings, NullLoggerFactory.Instance);
    }

    public void Dispose()
    {
        _host.Dispose();
        _container.Dispose();
    }

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

    [Fact]
    public void RootSingletonsSurviveWorldDisposalAndDisposeOnce()
    {
        var connection = new LocalConnection(Serializers);
        _pendingJoins.Add(connection.Server);
        connection.Client.Send(new JoinRequest
        {
            LevelGuid = "A",
            CharacterId = 100,
            PublicView = new PublicView { CharacterId = 100, Name = "P", Body = "wb:m_human" },
        });

        _update.Invoke(_host, [1f / 64f]);
        Assert.True(_host.PlayerCount == 1, "A world must be loaded for the disposal path to be exercised.");

        var probe = _container.Resolve<RootDisposable>();
        Assert.Equal(0, probe.Disposals);

        //  Host shutdown disposes every world container; the root singleton must remain untouched.
        _host.Dispose();
        Assert.Equal(0, probe.Disposals);

        //  Only the root container dispose may dispose it, exactly once.
        _container.Dispose();
        Assert.Equal(1, probe.Disposals);
    }
}