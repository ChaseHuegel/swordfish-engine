using System;
using DryIoc;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Physics.Jolt;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Server.Core.Systems;
using WaywardBeyond.Shared.Bricks;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// The authoritative server composition: every per-world service and system registers here as a
/// transient template, and each world resolves and pins its own instances into an exclusive child
/// container (see <see cref="ServerWorld"/>), so no world's graph can leak into another or into the
/// client runtime. Systems register under the <see cref="IServerWorldSystem"/> marker (never
/// <see cref="IEntitySystem"/>), so the client's ECS context cannot resolve them; third-party modules
/// contribute world systems through <see cref="RegisterServerSystem{T}"/>, which appends them after the
/// built-ins. Registration order is the world's tick order: join/disconnect → world management →
/// replication apply → inventory → heartbeats → physics → interaction → chat → replication publish.
/// Per-world services are wired with <c>Made.Of</c> factories rather than scoped <c>RegisterDelegate</c>
/// or <c>Reuse.Scoped</c>: DryIoc's compiled scoped-factory path is unusable on current runtimes.
/// </summary>
public static class ServerComposition
{
    public static void Register(IContainer container)
    {
        container.Register<World>();
        container.Register<ServerConnectionHub>(made: Made.Of(() => CreateConnectionHub(Arg.Of<NetworkingSettings>())));
        container.Register<SessionManager>();
        container.Register<WorldSaveService>();
        container.Register<ServerJoinQueue>();
        container.Register<NetworkReplicationSystem>();
        container.Register<ServerSkillSystem>();
        container.Register<JoltPhysicsSystem>();
        //  The step is disposable and transient by template; each world pins and disposes its own instance.
        container.Register<SharedSimulationStep>(
            made: Made.Of(() => CreateSimulationStep(Arg.Of<World>(), Arg.Of<JoltPhysicsSystem>())),
            setup: Setup.With(allowDisposableTransient: true)
        );
        container.Register<Func<DataStore, IVoxelInteractionWorld>>(made: Made.Of(() => CreateInteractionWorldFactory()));

        //  World systems in canonical tick order.
        RegisterServerSystem<ServerJoinSystem>(container);
        RegisterServerSystem<ServerWorldSystem>(container);
        RegisterServerSystem<NetworkApplySystem>(container);
        RegisterServerSystem<ServerInventorySystem>(container);
        RegisterServerSystem<ServerHeartbeatService>(container);
        RegisterServerSystem<ServerPhysicsSystem>(container);
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

    private static SharedSimulationStep CreateSimulationStep(World world, in JoltPhysicsSystem physics)
    {
        return new SharedSimulationStep(world.DataStore, physics);
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