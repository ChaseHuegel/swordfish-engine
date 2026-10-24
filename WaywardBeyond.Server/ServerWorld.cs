using System;
using DryIoc;
using Swordfish.ECS;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;

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

    /// <summary>The shared interaction handler registry this world resolves through.</summary>
    internal IInteractionHandlerRegistry HandlerRegistry { get; }

    private readonly IContainer _container;

    /// <summary>Resolves and pins this world's graph, then registers its systems with the world.</summary>
    public ServerWorld(IContainer container)
    {
        _container = container;
        World = Pin<World>(container);
        Sessions = Pin<SessionManager>(container);
        SaveService = Pin<LevelSaveService>(container);
        JoinQueue = Pin<ServerJoinQueue>(container);
        Replication = Pin<NetworkReplicationSystem>(container);
        //  Shared with every other world, so resolve it from the root instead of pinning a child copy.
        HandlerRegistry = container.Resolve<IInteractionHandlerRegistry>();
        Pin<IUserPermissionService>(container);
        Pin<IServerWorldPhysics>(container);
        Pin<SimulationStep>(container);
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