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
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;

namespace WaywardBeyond.Server;

/// <summary>
/// The authoritative server composition: every per-world service and system registers here as a
/// transient template, and each world resolves and pins its own instances into an exclusive child
/// container (see <see cref="ServerWorld"/>), so no world's graph can leak into another or into the
/// client runtime. Systems register under the <see cref="IServerWorldSystem"/> marker (never
/// <see cref="IEntitySystem"/>), so the client's ECS context cannot resolve them; third-party modules
/// contribute world systems through <see cref="RegisterServerSystem{T}"/>, which appends them after the
/// built-ins. Registration order is the world's tick order: join/disconnect → level management →
/// replication apply → inventory → heartbeats → physics → interaction → chat → replication publish.
/// Per-world services are wired with <c>Made.Of</c> factories rather than scoped <c>RegisterDelegate</c>
/// or <c>Reuse.Scoped</c>: DryIoc's compiled scoped-factory path is unusable on current runtimes.
/// </summary>
public static class ServerComposition
{
    public static void Register(IContainer container)
    {
        //  Server-level menu facade: create/list/delete saved levels for connections that have not
        //  joined a level yet; ticked by the host before level routing.
        container.Register<ServerLevelManager>(Reuse.Singleton);
        container.Register<ServerHostHeartbeat>(Reuse.Singleton);
        container.Register<PendingLevelDeletes>(Reuse.Singleton);
        container.Register<ILevelCatalog, SqliteLevelCatalog>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);

        //  Permissions: parse module and admin files once at startup; each world binds its own claims.
        container.RegisterTomlParser<PermissionFile>();
        container.Register<PermissionFileLoader>(Reuse.Singleton);
        container.RegisterDelegate<IPermissionPolicy>(context =>
        {
            PermissionFileLoader loader = context.Resolve<PermissionFileLoader>();
            var diagnostics = new PermissionDiagnostics();
            IReadOnlyList<SourcedPermissionFile> files = loader.Load(diagnostics);
            return PermissionPolicy.Create(files, diagnostics);
        }, Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.Register<IUserPermissionService, UserPermissionService>();
        container.Register<IEntryPoint, PermissionEntryPoint>(Reuse.Singleton);

        //  The server reads the level autosave cadence from gameplay.toml. The client registers the
        //  same file earlier in host mode, so the first registration is kept.
        container.RegisterConfig<GameplaySettings>(file: "gameplay.toml");

        container.Register<World>();
        container.Register<ServerConnectionHub>(made: Made.Of(() => CreateConnectionHub(Arg.Of<NetworkingSettings>())));
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
        container.Register<SharedSimulationStep>(
            made: Made.Of(() => CreateSimulationStep(Arg.Of<World>(), Arg.Of<IServerWorldPhysics>())),
            setup: Setup.With(allowDisposableTransient: true)
        );
        container.Register<Func<DataStore, IVoxelInteractionWorld>>(made: Made.Of(() => CreateInteractionWorldFactory()));

        //  World systems in canonical tick order.
        RegisterServerSystem<ServerJoinSystem>(container);
        RegisterServerSystem<ServerWorldSystem>(container);
        RegisterServerSystem<NetworkApplySystem>(container);
        RegisterServerSystem<ServerInventorySystem>(container);
        RegisterServerSystem<ServerHeartbeatService>(container);
        //  The physics slot is the world's physics bundle itself (same per-world instance as the step's).
        container.Register<IServerWorldSystem>(made: Made.Of(() => CreatePhysicsSlot(Arg.Of<IServerWorldPhysics>())), ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        //  ServerInteractionSystem keeps public convenience ctors for tests; the world graph pins the
        //  full one explicitly, and its marker registration passes the same concrete instance through
        //  (the join system depends on the concrete type).
        container.Register<ServerInteractionSystem>(
            made: Made.Of(() => new ServerInteractionSystem(
                Arg.Of<ServerConnectionHub>(),
                Arg.Of<IInteractionContent>(),
                Arg.Of<ILogger<ServerInteractionSystem>>(),
                Arg.Of<IInteractionHandlerRegistry>(),
                Arg.Of<Func<DataStore, IVoxelInteractionWorld>>(),
                Arg.Of<IBrickIdMap>(),
                Arg.Of<ServerSkillSystem>(),
                Arg.Of<SharedSimulationStep>()
            ))
        );
        container.Register<IServerWorldSystem>(made: Made.Of(() => CreateInteractionSystem(Arg.Of<ServerInteractionSystem>())));
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

    private static ServerConnectionHub CreateConnectionHub(in NetworkingSettings settings)
    {
        return new ServerConnectionHub(settings.MaxReceiveWindow.Get());
    }

    private static SharedSimulationStep CreateSimulationStep(World world, in IServerWorldPhysics physics)
    {
        return new SharedSimulationStep(world.DataStore, physics);
    }

    private static IServerWorldPhysics CreateWorldPhysics(ILoggerFactory loggerFactory, in PhysicsSettings physicsSettings)
    {
        return new ServerPhysicsSystem(loggerFactory, physicsSettings);
    }

    private static IServerWorldSystem CreatePhysicsSlot(in IServerWorldPhysics physics)
    {
        return (IServerWorldSystem)physics;
    }

    private static Func<DataStore, IVoxelInteractionWorld> CreateInteractionWorldFactory()
    {
        return store => new ServerVoxelInteractionWorld(store);
    }

    private static IServerWorldSystem CreateInteractionSystem(in ServerInteractionSystem interaction)
    {
        return interaction;
    }
}