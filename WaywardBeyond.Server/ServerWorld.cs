using System;
using DryIoc;
using Krill;
using WaywardBeyond.Server.Particles;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Stations;

namespace WaywardBeyond.Server;

/// <summary>
/// The per-world composition root. Wraps one world container created by the host and resolves the
/// world's graph from it: the engine <see cref="World"/> and store, the hub, sessions, save service,
/// join queue, and the ordered <see cref="IServerWorldSystem"/> list in registration order. Each
/// per-world service is resolved once and pinned into the world container as an instance, so every
/// system's constructor dependencies resolve to this world's own graph. Systems tick through the engine
/// world; nothing here hardcodes a system type.
/// </summary>
public sealed class ServerWorld : IDisposable
{
    /// <summary>The engine world this container owns.</summary>
    public World World { get; }
    /// <summary>The world data store. Only the server thread may touch it.</summary>
    public DataStore Store => World.DataStore;
    /// <summary>The world level save service.</summary>
    public LevelSaveService SaveService { get; }
    /// <summary>The world session registry.</summary>
    public SessionManager Sessions { get; }
    /// <summary>The world inbound join and leave queue.</summary>
    public ServerJoinQueue JoinQueue { get; }
    /// <summary>The world replication stages.</summary>
    public NetworkReplicationSystem Replication { get; }
    /// <summary>The world server-managed brick-entity registry and lifecycle service, or null in minimal test containers.</summary>
    public ServerBrickEntityService? BrickEntities { get; }
    /// <summary>The world active station binding and authorization service, or null in minimal test containers.</summary>
    public ServerStationService? Stations { get; }
    /// <summary>The world particle replication and burst service.</summary>
    public IServerParticleService Particles { get; }
    /// <summary>The shared simulation step for this world.</summary>
    internal SimulationStep SimulationStep { get; }

    /// <summary>The shared interaction handler registry this world resolves through.</summary>
    internal IInteractionHandlerRegistry HandlerRegistry { get; }

    /// <summary>The shared station action handler registry this world dispatches through, or null in minimal test containers.</summary>
    internal IStationActionHandlerRegistry? StationActionRegistry { get; }

    private readonly IContainer _container;

    /// <summary>Resolves and pins this world's graph, then registers its systems with the world.</summary>
    public ServerWorld(IContainer container)
    {
        _container = container;
        World = Pin<World>(container);
        Sessions = Pin<SessionManager>(container);
        JoinQueue = Pin<ServerJoinQueue>(container);
        Replication = Pin<NetworkReplicationSystem>(container);
        Pin<IUserPermissionService>(container);
        BrickEntities = container.IsRegistered<ServerBrickEntityService>()
            ? Pin<ServerBrickEntityService>(container)
            : null;
        Stations = container.IsRegistered<ServerStationService>()
            ? Pin<ServerStationService>(container)
            : null;
        if (Stations != null)
        {
            container.RegisterInstance<IStationSession>(Stations, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        }

        SaveService = Pin<LevelSaveService>(container);
        ServerParticleService particles = Pin<ServerParticleService>(container);
        container.RegisterInstance<IServerParticleService>(particles, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        Particles = particles;
        //  Shared with every other world, so resolve it from the root instead of pinning a child copy.
        HandlerRegistry = container.Resolve<IInteractionHandlerRegistry>();
        StationActionRegistry = container.IsRegistered<IStationActionHandlerRegistry>()
            ? container.Resolve<IStationActionHandlerRegistry>()
            : null;
        Pin<IServerWorldPhysics>(container);
        SimulationStep = Pin<SimulationStep>(container);
        Pin<Func<DataStore, IVoxelInteractionWorld>>(container);
        Pin<ServerInteractionSystem>(container);

        foreach (IServerWorldSystem system in container.ResolveMany<IServerWorldSystem>())
        {
            World.AddSystem(system);
        }
    }

    /// <summary>Ticks every world system once. Runs on the server thread.</summary>
    public void Tick(float delta)
    {
        World.Tick(delta);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _container.Dispose();
    }

    private static T Pin<T>(IContainer container) where T : notnull
    {
        T instance = container.Resolve<T>();
        container.RegisterInstance(instance);
        return instance;
    }
}