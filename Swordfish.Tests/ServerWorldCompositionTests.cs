using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DryIoc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Settings;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Server.Core.Systems;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;
using WaywardBeyond.Shared.Skills;
using Xunit;

namespace Swordfish.Tests;

public class ServerWorldCompositionTests
{
    private sealed class Fixture : IDisposable
    {
        public IContainer Container { get; }

        public Fixture()
        {
            Container = new Container();
            Container.RegisterInstance(new NetworkingSettings());
            Container.RegisterInstance(new PhysicsSettings());
            Container.RegisterInstance(TestBricks.Map);
            Container.RegisterInstance<ILoggerFactory>(NullLoggerFactory.Instance);
            Container.RegisterInstance<IInteractionContent>(new StubContent());
            Container.RegisterInstance<SkillDatabase>(SkillDatabaseTests.CreateSharedSkillDatabase());
            Container.RegisterDelegate<ILogger>(_ => NullLogger.Instance);
            MethodInfo createLogger = typeof(LoggerFactoryExtensions).GetMethod("CreateLogger", [typeof(ILoggerFactory)])!;
            Container.Register(typeof(ILogger<>), made: Made.Of(req => createLogger.MakeGenericMethod(req.Parent.ImplementationType)));
            Container.RegisterDelegate<Func<KeyValueStore>>(_ => () => throw new NotImplementedException());

            ServerComposition.Register(Container);
        }

        public ServerWorld OpenWorld()
        {
            //  withDisposables: false: a child container disposal must never cascade to the root's
            //  singletons; it still disposes the instances pinned into the world (see ServerWorld).
            return new ServerWorld(ContainerTools.CreateChild(Container, RegistrySharing.CloneAndDropCache, null, null, null, withDisposables: false));
        }

        public void Dispose()
        {
            Container.Dispose();
        }
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
    public void ResolvesPerWorldGraphsThatAreIsolatedPerScope()
    {
        using Fixture fixture = new();

        using ServerWorld first = fixture.OpenWorld();
        using ServerWorld second = fixture.OpenWorld();

        Assert.NotSame(first.World, second.World);
        Assert.NotSame(first.Hub, second.Hub);
        Assert.NotSame(first.Sessions, second.Sessions);
        Assert.NotSame(first.JoinQueue, second.JoinQueue);
    }

    [Fact]
    public void RegistersWorldSystemsInCanonicalTickOrder()
    {
        using Fixture fixture = new();

        Type[] expected =
        [
            typeof(ServerJoinSystem),
            typeof(ServerWorldSystem),
            typeof(NetworkApplySystem),
            typeof(ServerInventorySystem),
            typeof(ServerHeartbeatService),
            typeof(ServerPhysicsSystem),
            typeof(ServerInteractionSystem),
            typeof(ServerChatSystem),
            typeof(NetworkPublishSystem),
        ];

        using IResolverContext scope = fixture.Container.OpenScope();
        Type[] actual = scope.ResolveMany<IServerWorldSystem>().Select(system => system.GetType()).ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void JoinDeliveredToItsWorldOnly()
    {
        using Fixture fixture = new();
        using ServerWorld first = fixture.OpenWorld();
        using ServerWorld second = fixture.OpenWorld();

        var serializers = new INetworkSerializer[]
        {
            new NsdMessageSerializer<JoinRequest>(),
            new NsdMessageSerializer<JoinAccept>(),
            new NsdMessageSerializer<WorldEntityAdd>(),
            new NsdMessageSerializer<WorldStreamComplete>(),
            new NsdMessageSerializer<WorldSnapshot>(),
        };
        var connection = new LocalConnection(serializers);
        Uuid clientId = first.Hub.Add(connection.Server);
        connection.Client.Send(new JoinRequest
        {
            CharacterId = 100,
            PublicView = new PublicView { CharacterId = 100, Name = "P", Body = "wb:m_human" },
        });

        first.Tick(1f / 60f);
        second.Tick(1f / 60f);

        Assert.True(connection.Client.Receive<JoinAccept>().Success, "The joining world must accept the client.");
        Assert.Equal(1, first.Sessions.Count);
        Assert.Equal(0, second.Sessions.Count);
    }
}