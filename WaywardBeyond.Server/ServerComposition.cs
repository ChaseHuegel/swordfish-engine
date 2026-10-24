using System;
using DryIoc;
using Microsoft.Extensions.Logging;
using Shoal.Extensions.Swordfish;
using Shoal.Modularity;
using Swordfish.ECS;
using Swordfish.Settings;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Permissions;
using WaywardBeyond.Skills;

namespace WaywardBeyond.Server;

/// <summary>
/// The authoritative server composition: every per-world service and system registers here as a
/// transient template, and each world resolves and pins its own instances into an exclusive child
/// container (see <see cref="ServerWorld"/>), so no world's graph can leak into another or into the
/// client runtime. Systems register under the <see cref="IServerWorldSystem"/> marker (never
/// <see cref="IEntitySystem"/>), so the client's ECS context cannot resolve them; third-party modules
/// contribute world systems through <see cref="RegisterServerSystem{T}"/>, which appends them after
/// the built-ins. Registration order is the world's tick order: join/disconnect → saves →
/// replication apply → inventory → heartbeats → physics → interaction → chat → replication publish.
/// </summary>
public static class ServerComposition
{
    public static void RegisterServer(this IContainer container)
    {
        container.RegisterDataServices();

        //  Server-level menu facade: create/list/delete saved levels for connections that have not
        //  joined a level yet; ticked by the host before level routing.
        container.Register<ServerLevelManager>(Reuse.Singleton);
        container.Register<PendingConnectionHeartbeat>(Reuse.Singleton);
        container.Register<ILevelCatalog, SqliteLevelCatalog>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.Register<ServerPeers>(Reuse.Singleton);
        container.Register<ServerHostCommands>(Reuse.Singleton);
        container.Register<ServerHandlers>(Reuse.Singleton);
        container.Register<ServerRpcHost>(Reuse.Singleton);

        //  Permissions: parse module and admin files once at startup; each world binds its own claims.
        container.RegisterPermissions();
        container.Register<PermissionResolver>(Reuse.Singleton);
        container.Register<IUserPermissionService, UserPermissionService>();
        container.Register<IEntryPoint, PermissionEntryPoint>(Reuse.Singleton);

        //  The default interaction content. A client module replaces it with richer content; a headless
        //  server keeps this one. The replacement is order-independent: Keep here, Replace in the client.
        container.Register<IInteractionContent, ServerInteractionContent>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);

        //  The server reads the level autosave cadence from gameplay.toml. The client registers the
        //  same file earlier in host mode, so the first registration is kept.
        container.RegisterConfig<GameplayConfig>(file: "gameplay.toml");

        //  Level generation tuning is server-side; the client never generates worlds.
        container.RegisterConfig<LevelGeneratorConfig>(file: "levelgen.toml");
        container.RegisterDelegate<AsteroidConfig>(context => context.Resolve<LevelGeneratorConfig>().Asteroid, Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.RegisterDataBinding<string[]>();

        container.Register<World>();
        container.Register<SessionManager>();
        //  The level save service holds an open save database; each world pins and disposes its own.
        container.Register<LevelSaveService>(setup: Setup.With(allowDisposableTransient: true));
        container.Register<ServerJoinQueue>();
        container.Register<NetworkReplicationSystem>();
        container.Register<ServerSkillSystem>();
        //  The world physics bundle is created directly (it owns the engine's Jolt system) and exposed
        //  under the game-owned identity, so nothing resolves the engine's concrete JoltPhysicsSystem
        //  (which the engine also registers as a root singleton).
        container.Register<IServerWorldPhysics>(made: Made.Of(() => CreateWorldPhysics(Arg.Of<ILoggerFactory>(), Arg.Of<PhysicsSettings>())), ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        //  The step is disposable and transient by template; each world pins and disposes its own instance.
        container.Register(
            made: Made.Of(() => CreateSimulationStep(Arg.Of<World>(), Arg.Of<IServerWorldPhysics>())),
            setup: Setup.With(allowDisposableTransient: true)
        );
        container.Register<Func<DataStore, IVoxelInteractionWorld>>(made: Made.Of(() => CreateInteractionWorldFactory()));

        //  World systems in canonical tick order.
        RegisterServerSystem<ServerJoinSystem>(container);
        RegisterServerSystem<ServerSaveSystem>(container);
        RegisterServerSystem<NetworkApplySystem>(container);
        RegisterServerSystem<ServerInventorySystem>(container);
        RegisterServerSystem<ServerHeartbeatService>(container);
        //  The physics slot is the world's physics bundle itself (same per-world instance as the step's).
        container.Register(made: Made.Of(() => CreatePhysicsSlot(Arg.Of<IServerWorldPhysics>())), ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.Register<ServerInteractionSystem>(
            made: Made.Of(() => new ServerInteractionSystem(
                Arg.Of<ServerPeers>(),
                Arg.Of<IInteractionContent>(),
                Arg.Of<ILogger<ServerInteractionSystem>>(),
                Arg.Of<IInteractionHandlerRegistry>(),
                Arg.Of<Func<DataStore, IVoxelInteractionWorld>>(),
                Arg.Of<IBrickRegistry>(),
                Arg.Of<ServerSkillSystem>(),
                Arg.Of<SimulationStep>()
            ))
        );
        container.Register(made: Made.Of(() => CreateInteractionSystem(Arg.Of<ServerInteractionSystem>())));
        RegisterServerSystem<ServerChatSystem>(container);
        RegisterServerSystem<NetworkPublishSystem>(container);

        //  The one interaction handler registry. Server gameplay and gameplay modules both register
        //  handlers into it; the authoritative ServerInteractionSystem runs them in order.
        container.Register<IInteractionHandlerRegistry, InteractionHandlerRegistry>(Reuse.Singleton);

        //  DryIoc children only inherit a parent singleton the parent has already created. Resolve
        //  every shared service a world graph consumes before any world container exists, so a world
        //  cannot silently get a private copy (a second handler registry would ignore registered
        //  handlers). New shared singletons belong on this list.
        PinSharedSingletons(container);
    }

    private static void PinSharedSingletons(IContainer container)
    {
        container.Resolve<NetworkRegistry>();
        container.Resolve<ILevelCatalog>();
        container.Resolve<IInteractionContent>();
        container.Resolve<IInteractionHandlerRegistry>();
        container.Resolve<PermissionPolicy>();
        container.Resolve<ServerPeers>();
        container.Resolve<IBrickRegistry>();
        container.Resolve<ISkillDatabase>();
    }

    /// <summary>
    /// Registers a server world system: each world resolves its own instance, ticked in registration
    /// order. Append systems after the built-ins to place them after the publish stage, or between the
    /// built-ins to slot earlier; the intra-world stage contract lives in the networking-worlds spec.
    /// </summary>
    public static void RegisterServerSystem<T>(this IContainer container) where T : class, IServerWorldSystem
    {
        container.Register<IServerWorldSystem, T>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
    }

    private static SimulationStep CreateSimulationStep(World world, IServerWorldPhysics physics)
    {
        return new SimulationStep(world.DataStore, physics);
    }

    private static IServerWorldPhysics CreateWorldPhysics(ILoggerFactory loggerFactory, PhysicsSettings physicsSettings)
    {
        return new ServerPhysicsSystem(loggerFactory, physicsSettings);
    }

    private static IServerWorldSystem CreatePhysicsSlot(IServerWorldPhysics physics)
    {
        return (IServerWorldSystem)physics;
    }

    private static Func<DataStore, IVoxelInteractionWorld> CreateInteractionWorldFactory()
    {
        return store => new ServerVoxelInteractionWorld(store);
    }

    private static IServerWorldSystem CreateInteractionSystem(ServerInteractionSystem interaction)
    {
        return interaction;
    }
}