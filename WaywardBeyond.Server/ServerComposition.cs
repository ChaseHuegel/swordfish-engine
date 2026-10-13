using System;
using System.Collections.Generic;
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
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;

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
        container.Register<PendingLevelDeletes>(Reuse.Singleton);
        container.Register<ILevelCatalog, SqliteLevelCatalog>(Reuse.Singleton);

        //  Permissions: parse module and admin files once at startup; each world binds its own claims.
        container.RegisterTomlParser<PermissionFile>();
        container.Register<PermissionFileLoader>(Reuse.Singleton);
        container.RegisterDelegate<PermissionPolicy>(context =>
        {
            PermissionFileLoader loader = context.Resolve<PermissionFileLoader>();
            var diagnostics = new PermissionDiagnostics();
            IReadOnlyList<SourcedPermissionFile> files = loader.Load(diagnostics);
            return PermissionPolicy.Create(files, diagnostics);
        }, Reuse.Singleton);
        container.Register<ConnectionClaims>(Reuse.Singleton);
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
        container.Register(made: Made.Of(() => CreateConnectionHub(Arg.Of<NetworkingConfig>())));
        container.Register<SessionManager>();
        //  The level save service holds an open save database; each world pins and disposes its own.
        container.Register<LevelSaveService>(setup: Setup.With(allowDisposableTransient: true));
        container.Register<ServerJoinQueue>();
        container.Register<NetworkReplicationSystem>();
        container.Register<ServerSkillSystem>();
        //  The world physics bundle is created directly (it owns the engine's Jolt system) and exposed
        //  under the game-owned identity, so nothing resolves the engine's concrete JoltPhysicsSystem
        //  (which the engine also registers as a root singleton).
        container.Register<IServerWorldPhysics>(made: Made.Of(() => CreateWorldPhysics(Arg.Of<ILoggerFactory>(), Arg.Of<PhysicsSettings>())));
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
                Arg.Of<ServerConnectionHub>(),
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

        //  Server-side interaction mod hooks: a single shared registry that server mods register their
        //  interaction handlers into, resolved by the authoritative ServerInteractionSystem.
        container.Register<IInteractionHandlerRegistry, InteractionHandlerRegistry>(Reuse.Singleton);
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

    private static ServerConnectionHub CreateConnectionHub(NetworkingConfig config)
    {
        return new ServerConnectionHub(config.Server.MaxReceiveWindow.Get());
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