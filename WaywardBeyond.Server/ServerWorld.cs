using System;
using DryIoc;
using Swordfish.ECS;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking.Transport;

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
    public World World { get; }
    public DataStore Store => World.DataStore;
    public ServerConnectionHub Hub { get; }
    public LevelSaveService SaveService { get; }
    public SessionManager Sessions { get; }
    public ServerJoinQueue JoinQueue { get; }

    private readonly IContainer _container;

    public ServerWorld(IContainer container)
    {
        _container = container;
        World = Pin<World>(container);
        Hub = Pin<ServerConnectionHub>(container);
        Sessions = Pin<SessionManager>(container);
        SaveService = Pin<LevelSaveService>(container);
        JoinQueue = Pin<ServerJoinQueue>(container);
        Pin<NetworkReplicationSystem>(container);
        Pin<ServerSkillSystem>(container);
        Pin<IUserPermissionService>(container);
        Pin<IServerWorldPhysics>(container);
        Pin<SharedSimulationStep>(container);
        Pin<Func<DataStore, IVoxelInteractionWorld>>(container);
        Pin<ServerInteractionSystem>(container);

        foreach (IServerWorldSystem system in container.ResolveMany<IServerWorldSystem>())
        {
            World.AddSystem(system);
        }
    }

    public void Tick(float delta)
    {
        World.Tick(delta);
    }

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