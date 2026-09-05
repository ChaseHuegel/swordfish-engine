using System;
using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using Swordfish.ECS;
using Swordfish.Library.Threading;
using WaywardBeyond.Server.Core.Systems;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// Hosts the authoritative server world and ticks its systems on a dedicated thread. In the current
/// singleplayer layout this runs in-process alongside the client world; in a networked layout it would
/// run standalone.
/// </summary>
public sealed class ServerContext : IEntryPoint, IDisposable
{
    public World World { get; }

    private readonly ThreadWorker _threadWorker;
    private readonly ILogger _logger;

    public ServerContext(in IServerConnection transport, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ServerContext>();
        _threadWorker = new ThreadWorker(Update, "Server");

        World = new World();
        World.AddSystem(new NetworkReplicationSystem(transport, loggerFactory.CreateLogger<NetworkReplicationSystem>()));
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
        World.Tick(delta);
    }
}