using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core.Systems;

/// <summary>
/// Server-authoritative spawn. Handles client <see cref="SpawnRequest"/>s by allocating a player
/// entity on the server world, wiring it for replication, and replying with the assigned uuid. The
/// server holds authority over which spawns it materializes; the client later seats the initial
/// transform as a placement and drives local motion.
/// </summary>
public sealed class ServerSpawnSystem : IEntitySystem
{
    private readonly IServerConnection _transport;
    private readonly ServerPlayerOwnership _ownership;
    private readonly ILogger<ServerSpawnSystem> _logger;

    public ServerSpawnSystem(
        in IServerConnection transport,
        in ServerPlayerOwnership ownership,
        in ILogger<ServerSpawnSystem> logger
    ) {
        _transport = transport;
        _ownership = ownership;
        _logger = logger;
    }

    public void Tick(float delta, DataStore store)
    {
        Result<SpawnRequest> receiveResult;
        while ((receiveResult = _transport.Receive<SpawnRequest>()).Success)
        {
            HandleSpawn(receiveResult.Value, store);
        }
    }

    private void HandleSpawn(SpawnRequest request, DataStore store)
    {
        int entity = store.Alloc();
        Uuid uuid = store.GetUuid(entity);

        store.AddOrUpdate(entity, new NetworkComponent());
        _ownership.SetOwnedPlayer(uuid);

        _transport.Send(new SpawnResponse { Entity = uuid.ToValue(), Accepted = true });

        _logger.LogInformation("Spawned player entity {uuid} for character {character}.", uuid, request.CharacterId);
    }
}