using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DryIoc;
using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using Swordfish.ECS;
using Swordfish.Library.Threading;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// Owns the server's world table and ticks every live world on a single server thread. Each tick it
/// routes pending connections to the hub of the world their <c>JoinRequest</c> names (creating and
/// loading the world first, so a join can never unload under itself), ticks each world's systems in
/// creation order, and unloads worlds with no sessions past the idle window. A world is a child
/// container created from the root; disposal of the host flushes and tears down every world
/// synchronously after the thread stops.
/// </summary>
public sealed class ServerWorldHost : IEntryPoint, IDisposable
{
    public int PlayerCount
    {
        get
        {
            int count = 0;
            foreach (WorldEntry entry in _worlds)
            {
                count += entry.World.Hub.Count;
            }

            return count;
        }
    }

    private readonly IContainer _container;
    private readonly ServerWorldManager _worldManager;
    private readonly PendingJoins _pendingJoins;
    private readonly ILogger _logger;
    private readonly long _idleUnloadMs;
    private readonly ThreadWorker _threadWorker;
    private readonly List<WorldEntry> _worlds = [];
    private readonly List<WorldEntry> _unloads = [];
    private readonly ConcurrentDictionary<IServerConnection, WorldBinding> _bindings = new();

    public ServerWorldHost(
        in IContainer container,
        in ServerWorldManager worldManager,
        in PendingJoins pendingJoins,
        in NetworkingSettings settings,
        ILoggerFactory loggerFactory
    ) {
        _container = container;
        _worldManager = worldManager;
        _pendingJoins = pendingJoins;
        _logger = loggerFactory.CreateLogger<ServerWorldHost>();
        _idleUnloadMs = Math.Max(1, settings.WorldIdleUnloadMs.Get());
        _threadWorker = new ThreadWorker(Update, "Server");
    }

    public void Run()
    {
        _threadWorker.Start();
        _logger.LogInformation("Started server thread.");
    }

    /// <summary>Releases a connection from its world (accepted peer disconnect). A pending connection is unchanged.</summary>
    public void DetachConnection(in IServerConnection connection)
    {
        if (_bindings.TryRemove(connection, out WorldBinding binding))
        {
            binding.World.Hub.Remove(binding.ClientId);
        }
    }

    public void Dispose()
    {
        _threadWorker.Stop();
        //  The tick thread may still be mid-Update (an idle unload, a join route); join before the
        //  worlds table is torn down so the loop below never races the server thread.
        _threadWorker.Join();

        //  The server thread owns the stores; once stopped, shutdown may safely flush them before the
        //  world containers and the local NATS backing are disposed.
        foreach (WorldEntry entry in _worlds)
        {
            foreach ((Uuid clientId, _) in entry.World.Hub.Clients)
            {
                entry.World.Sessions.EndSession(clientId);
                entry.World.Hub.Remove(clientId);
            }

            entry.World.WorldService.Flush(entry.World.Store);
            entry.World.Dispose();
        }

        _worlds.Clear();
        _bindings.Clear();
        _logger.LogInformation("Stopped server thread.");
    }

    private void Update(float delta)
    {
        try
        {
            _worldManager.Tick();
            RoutePendingJoins();

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

    private void RoutePendingJoins()
    {
        foreach (IServerConnection connection in _pendingJoins.Snapshot())
        {
            Result<JoinRequest> result = connection.Receive<JoinRequest>();
            if (!result.Success)
            {
                continue;
            }

            ServerWorld world = GetOrCreate(result.Value.LevelGuid ?? string.Empty);
            Uuid clientId = AttachConnection(connection, world);
            world.JoinQueue.Enqueue(clientId, result.Value);
            _pendingJoins.Remove(connection);
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

        IContainer worldContainer = ContainerTools.CreateChild(_container, RegistrySharing.CloneAndDropCache, null, null, null, withDisposables: true);
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
            world.WorldService.LoadLevel(levelGuid, world.Store);
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

    /// <summary>Binds a connection to a world's hub, migrating it out of any other world first.</summary>
    private Uuid AttachConnection(in IServerConnection connection, ServerWorld world)
    {
        if (_bindings.TryRemove(connection, out WorldBinding previous))
        {
            previous.World.Hub.Remove(previous.ClientId);
        }

        Uuid clientId = world.Hub.Add(connection);
        _bindings[connection] = new WorldBinding(world, clientId);
        return clientId;
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
        entry.World.WorldService.QueueWorldSave(entry.World.Store);

        //  Its connections are unbound and await a future join; the singleplayer loopback returns here.
        foreach (IServerConnection connection in _bindings.Keys)
        {
            if (_bindings.TryGetValue(connection, out WorldBinding binding) && binding.World == entry.World)
            {
                _bindings.TryRemove(connection, out _);
                _pendingJoins.Add(connection);
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

    private readonly struct WorldBinding(in ServerWorld world, in Uuid clientId)
    {
        public readonly ServerWorld World = world;
        public readonly Uuid ClientId = clientId;
    }
}