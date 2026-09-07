using System;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using Swordfish.ECS;
using Swordfish.Library.Threading;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Server.Core.Systems;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// Hosts the authoritative server world and ticks its systems on a dedicated thread. The server runs its
/// own physics world and the shared player-motion step, mirroring the client's runtime config so
/// authority and prediction don't diverge. Systems are ticked in an explicit order:
/// spawn → replication apply → physics (runs the shared step per fixed step) → disconnect teardown →
/// replication publish. Multiple clients are served through a <see cref="ServerConnectionHub"/> and routed
/// by <see cref="SessionManager"/>; in the current singleplayer layout this runs in-process alongside the
/// client world as the N=1 case.
/// </summary>
public sealed class ServerContext : IEntryPoint, IDisposable
{
    public World World { get; }

    private readonly ThreadWorker _threadWorker;
    private readonly ILogger _logger;

    private readonly ServerConnectionHub _hub;
    private readonly SessionManager _sessions;
    private readonly ServerWorldSystem _world;
    private readonly ServerJoinSystem _join;
    private readonly NetworkReplicationSystem _replication;
    private readonly JoltPhysicsSystem _physics;
    private readonly SharedPlayerMotionStep _motionStep;
    private readonly WorldSaveService _worldService;

    public ServerContext(
        in ServerConnectionHub hub,
        SessionManager sessions,
        in PhysicsSettings physicsSettings,
        in Func<KeyValueStore> keyValueStore,
        ILoggerFactory loggerFactory
    ) {
        _logger = loggerFactory.CreateLogger<ServerContext>();
        _threadWorker = new ThreadWorker(Update, "Server");

        _hub = hub;
        _sessions = sessions;
        World = new World();

        _worldService = new WorldSaveService(loggerFactory.CreateLogger<WorldSaveService>(), keyValueStore);
        _world = new ServerWorldSystem(hub, _worldService, loggerFactory.CreateLogger<ServerWorldSystem>());
        _join = new ServerJoinSystem(hub, sessions, _worldService, loggerFactory.CreateLogger<ServerJoinSystem>());
        _replication = new NetworkReplicationSystem(hub, sessions, loggerFactory.CreateLogger<NetworkReplicationSystem>());

        _physics = new JoltPhysicsSystem(loggerFactory.CreateLogger<JoltPhysicsSystem>(), physicsSettings);
        //  Mirror the client's physics runtime config (gravity zero, by default a fresh world is Earth).
        _physics.SetGravity(Vector3.Zero);

        _motionStep = new SharedPlayerMotionStep(World.DataStore, _physics, ResolveCommand);
    }

    public void Run()
    {
        _threadWorker.Start();
        _logger.LogInformation("Started server thread.");
    }

    public void Dispose()
    {
        _threadWorker.Stop();

        //  The server thread owns the store; once stopped this disposing thread may safely capture and
        //  persist the authoritative world synchronously before the local NATS backing is disposed.
        _worldService.Flush(World.DataStore);

        //  Tear down any remaining sessions and their connections for the (in-process N=1) shutdown.
        foreach ((Uuid clientId, _) in _hub.Clients)
        {
            _sessions.EndSession(clientId);
            _hub.Remove(clientId);
        }

        _logger.LogInformation("Stopped server thread.");
    }

    private void Update(float delta)
    {
        try
        {
            DataStore store = World.DataStore;

            HandleDisconnects(store);

            _world.Tick(delta, store);
            _join.Tick(delta, store);
            _replication.ApplyStage(delta, store);
            _physics.Tick(delta, store);

            _replication.SimTick = _motionStep.CurrentSimTick;
            _replication.PublishStage(delta, store);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in the server tick loop.");
        }
    }

    /// <summary>
    /// Ends dropped client sessions: disposes the mirror's physics body (marshalled to the physics thread
    /// via its <see cref="PhysicsComponent.Dispose"/>) before freeing the entity so the subsequent publish
    /// stage replicates its despawn to remaining clients, then clears the session mappings.
    /// </summary>
    private void HandleDisconnects(DataStore store)
    {
        foreach (Uuid clientId in _hub.DrainDisconnects())
        {
            if (_sessions.TryGetEntity(clientId, out int entity))
            {
                if (store.TryGet(entity, out PhysicsComponent physics))
                {
                    physics.Dispose();
                }

                //  Capture the uuid before Free (which clears it) so the despawn can replicate.
                _replication.RequestDespawn(store.GetUuid(entity).ToValue());
                store.Free(entity);
            }

            _sessions.EndSession(clientId);
            _logger.LogInformation("Ended session for client {client}.", clientId);
        }
    }

    private bool ResolveCommand(int entity, uint simTick, out InputComponent command)
    {
        if (World.DataStore.TryGet(entity, out NetworkComponent net) && net.StagedInputs != null)
        {
            return net.StagedInputs.TryGet(simTick, out command);
        }

        command = default;
        return false;
    }
}