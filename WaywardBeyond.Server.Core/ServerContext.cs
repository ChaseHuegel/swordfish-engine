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
/// spawn → replication apply → physics (runs the shared step per fixed step) → replication publish.
/// In the current singleplayer layout this runs in-process alongside the client world.
/// </summary>
public sealed class ServerContext : IEntryPoint, IDisposable
{
    public World World { get; }

    private readonly ThreadWorker _threadWorker;
    private readonly ILogger _logger;

    private readonly ServerSpawnSystem _spawn;
    private readonly NetworkReplicationSystem _replication;
    private readonly JoltPhysicsSystem _physics;
    private readonly SharedPlayerMotionStep _motionStep;
    private readonly ServerWorldService _worldService;

    public ServerContext(
        in IServerConnection transport,
        in PhysicsSettings physicsSettings,
        in Func<KeyValueStore> keyValueStore,
        ILoggerFactory loggerFactory
    ) {
        _logger = loggerFactory.CreateLogger<ServerContext>();
        _threadWorker = new ThreadWorker(Update, "Server");

        World = new World();

        _worldService = new ServerWorldService(loggerFactory.CreateLogger<ServerWorldService>(), keyValueStore);
        _spawn = new ServerSpawnSystem(transport, _worldService, loggerFactory.CreateLogger<ServerSpawnSystem>());
        _replication = new NetworkReplicationSystem(transport, loggerFactory.CreateLogger<NetworkReplicationSystem>());

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
        _logger.LogInformation("Stopped server thread.");
    }

    private void Update(float delta)
    {
        try
        {
            DataStore store = World.DataStore;

            _spawn.Tick(delta, store);
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