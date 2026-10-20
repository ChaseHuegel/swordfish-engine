using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DryIoc;
using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using NsdRpc;
using Swordfish.ECS;
using Swordfish.Library.Threading;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Config;
using WaywardBeyond.Data;

namespace WaywardBeyond.Server;

/// <summary>
/// Owns the server's world table and ticks every live world on a single server thread. Each tick it
/// drains host RPC commands, routes joins and leaves, ticks each world's systems in creation order,
/// and unloads worlds with no sessions past the idle window. A world is a child container created
/// from the root. Disposal of the host flushes and tears down every world synchronously after the
/// thread stops.
/// </summary>
public sealed class ServerWorldHost : IEntryPoint, IDisposable
{
    public int PlayerCount
    {
        get
        {
            //  Read from the server thread and the LAN beacon thread: snapshot so a concurrent
            //  join/unload cannot invalidate the enumeration.
            int count = 0;
            foreach (WorldEntry entry in _worlds.ToArray())
            {
                count += entry.World.Sessions.Count;
            }

            return count;
        }
    }

    private readonly IContainer _container;
    private readonly ServerHostCommands _commands;
    private readonly PendingConnectionHeartbeat _hostHeartbeat;
    private readonly ServerPeers _peers;
    private readonly ILevelCatalog _levelCatalog;
    private readonly ILogger _logger;
    private readonly long _idleUnloadMs;
    private readonly ThreadWorker _threadWorker;
    private readonly List<WorldEntry> _worlds = [];
    private readonly List<WorldEntry> _unloads = [];
    private bool _disposed;

    public ServerWorldHost(
        IContainer container,
        ServerHostCommands commands,
        PendingConnectionHeartbeat hostHeartbeat,
        ServerPeers peers,
        ILevelCatalog levelCatalog,
        NetworkingConfig config,
        ILoggerFactory loggerFactory
    ) {
        _container = container;
        _commands = commands;
        _hostHeartbeat = hostHeartbeat;
        _peers = peers;
        _levelCatalog = levelCatalog;
        _logger = loggerFactory.CreateLogger<ServerWorldHost>();
        _idleUnloadMs = Math.Max(1, config.Server.IdleUnloadMs.Get());
        _threadWorker = new ThreadWorker(Update, "Server");
    }

    public void Run()
    {
        _threadWorker.Start();
        _logger.LogInformation("Started server thread.");
    }

    /// <summary>
    /// Routes one join to the world it names. The world is created and loaded first, so a join can
    /// never unload under itself. The completion fires when the world has seated the player.
    /// </summary>
    public void RouteJoin(
        ServerPeer peer,
        in JoinRequest request,
        TaskCompletionSource<RpcResult<JoinAccept>> completion,
        CancellationToken cancellationToken
    ) {
        ServerWorld world = GetOrCreate(request.LevelGuid ?? string.Empty);
        if (peer.World != null && peer.World != world)
        {
            //  End the previous session in its own world; this peer is moving to a new one.
            peer.World.JoinQueue.EnqueueLeave(peer.GameId);
        }

        peer.ClearInbound();
        peer.World = world;
        world.JoinQueue.Enqueue(peer, request, completion, cancellationToken);
    }

    /// <summary>Queues the peer's leave in its own world. The world tick frees the mirror.</summary>
    public void RouteLeave(ServerPeer peer)
    {
        peer.World?.JoinQueue.EnqueueLeave(peer.GameId);
    }

    /// <summary>
    /// Tears down a loaded world without a final save, then removes the level from the catalog. Runs
    /// on the server thread. Returns the catalog result.
    /// </summary>
    /// <summary>The loaded world for a level, or null. Tests use it to seed world content.</summary>
    internal ServerWorld? FindWorld(string levelGuid)
    {
        foreach (WorldEntry entry in _worlds)
        {
            if (entry.LevelGuid == levelGuid)
            {
                return entry.World;
            }
        }

        return null;
    }

    public bool DeleteLevel(string levelGuid)
    {
        for (var index = 0; index < _worlds.Count; index++)
        {
            WorldEntry entry = _worlds[index];
            if (entry.LevelGuid != levelGuid)
            {
                continue;
            }

            foreach (ServerPeer peer in _peers.Peers)
            {
                if (peer.World == entry.World)
                {
                    peer.ClearInbound();
                    peer.World = null;
                }
            }

            entry.World.Dispose();
            _worlds.RemoveAt(index);
            _logger.LogInformation("Unloaded level {level} for deletion.", levelGuid);
            break;
        }

        return _levelCatalog.Delete(levelGuid);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _threadWorker.Stop();
        //  The tick thread may still be mid-Update (an idle unload, a join route); join before the
        //  worlds table is torn down so the loop below never races the server thread.
        _threadWorker.Join();

        //  The server thread owns the stores; once stopped, shutdown may safely flush them before the
        //  world containers are disposed. The list is snapshotted so a world container disposal can
        //  never invalidate the enumerator.
        foreach (WorldEntry entry in _worlds.ToArray())
        {
            foreach (ServerPeer peer in _peers.Peers)
            {
                if (peer.World == entry.World)
                {
                    entry.World.Sessions.EndSession(peer.GameId);
                    peer.World = null;
                }
            }

            entry.World.SaveService.Flush(entry.World.Store);
            entry.World.Dispose();
        }

        _worlds.Clear();
        _logger.LogInformation("Stopped server thread.");
    }

    private void Update(float delta)
    {
        try
        {
            //  Process host calls first so joins and leaves reach the worlds in this tick.
            _commands.Tick(this);
            HandlePeerDisconnects();
            _hostHeartbeat.Tick((uint)PlayerCount);

            //  Unloads are deferred until after the world iteration: removing from _worlds during the
            //  tick loop would invalidate the enumerator.
            foreach (WorldEntry entry in _worlds)
            {
                entry.World.Tick(delta);
                if (ShouldUnload(entry))
                {
                    _unloads.Add(entry);
                }
            }

            ReturnSessionlessConnectionsToPending();

            foreach (WorldEntry entry in _unloads)
            {
                UnloadWorld(entry);
            }

            _unloads.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in the server tick loop.");
        }
    }

    /// <summary>
    /// Queues a disconnect teardown in the peer's world. The world tick frees the mirror and replicates
    /// the despawn. A peer with no world needs no teardown beyond the peer record.
    /// </summary>
    private void HandlePeerDisconnects()
    {
        while (_peers.TryDequeueDisconnected(out ServerPeer peer))
        {
            ServerWorld? world = peer.World;
            peer.World = null;
            peer.ClearInbound();
            world?.JoinQueue.EnqueueDisconnect(peer.GameId);
        }
    }

    /// <summary>
    /// Returns peers whose session has ended (a player left to the menu) to the unbound state, so
    /// their next join is routed to the world it names. A bound peer without a session is idle, never
    /// mid-join: joins are enqueued and registered within the same world tick.
    /// </summary>
    private void ReturnSessionlessConnectionsToPending()
    {
        foreach (ServerPeer peer in _peers.Peers)
        {
            if (peer.World == null || peer.World.Sessions.TryGetEntity(peer.GameId, out _))
            {
                continue;
            }

            peer.ClearInbound();
            peer.World = null;
        }
    }

    private ServerWorld GetOrCreate(string levelGuid)
    {
        foreach (WorldEntry entry in _worlds)
        {
            if (entry.LevelGuid == levelGuid)
            {
                return entry.World;
            }
        }

        IContainer worldContainer = ContainerTools.CreateChild(_container, RegistrySharing.CloneAndDropCache, null, null, null, withDisposables: false);
        ServerWorld world;
        try
        {
            world = new ServerWorld(worldContainer);
        }
        catch
        {
            worldContainer.Dispose();
            throw;
        }

        try
        {
            world.SaveService.LoadLevel(levelGuid, world.Store);
        }
        catch (Exception ex)
        {
            //  A failed load leaves a fresh empty world; the join still proceeds at the default spawn,
            //  matching the pre-multiworld join behavior. The failure is isolated to this world.
            _logger.LogError(ex, "Failed to load level {level} for a new world.", levelGuid);
        }

        _worlds.Add(new WorldEntry(levelGuid, world));
        _logger.LogInformation("Loaded world {level}.", levelGuid);
        return world;
    }

    private bool ShouldUnload(WorldEntry entry)
    {
        if (entry.World.Sessions.Count > 0)
        {
            entry.IdleSinceMs = 0;
            return false;
        }

        long now = Environment.TickCount64;
        if (entry.IdleSinceMs == 0)
        {
            entry.IdleSinceMs = now;
            return false;
        }

        return now - entry.IdleSinceMs >= _idleUnloadMs;
    }

    private void UnloadWorld(WorldEntry entry)
    {
        //  Final authoritative flush: captured on the server thread, persisted in the background.
        entry.World.SaveService.QueueSave(entry.World.Store);

        foreach (ServerPeer peer in _peers.Peers)
        {
            if (peer.World == entry.World)
            {
                peer.ClearInbound();
                peer.World = null;
            }
        }

        entry.World.Dispose();
        _worlds.Remove(entry);
        _logger.LogInformation("Unloaded world {level}.", entry.LevelGuid);
    }

    private sealed class WorldEntry(in string levelGuid, in ServerWorld world)
    {
        public readonly string LevelGuid = levelGuid;
        public readonly ServerWorld World = world;
        public long IdleSinceMs;
    }
}
