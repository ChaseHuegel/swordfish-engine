using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Voxels.Building;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Submits a <see cref="SpawnRequest"/> on behalf of a local player and, once the server assigns an
/// entity uuid, materializes the local player. The server owns body construction and the initial
/// transform, which arrives through the authoritative snapshot path; the client never authors
/// ServerOwned state. Requests are handed off over a queue so the load thread never mutates state the
/// ECS thread reads; each request is sent at most once.
/// </summary>
internal sealed class ClientPlayerSpawnSystem : IEntitySystem
{
    private readonly IClientConnection _transport;
    private readonly PlayerCharacterEntityBuilder _playerBuilder;
    private readonly ILogger<ClientPlayerSpawnSystem> _logger;

    private readonly ConcurrentQueue<PlayerSpawnRequest> _requests = new();
    private PlayerSpawnRequest? _pending;
    private bool _sent;
    private bool _spawned;

    public ClientPlayerSpawnSystem(
        in IClientConnection transport,
        in PlayerCharacterEntityBuilder playerBuilder,
        ILogger<ClientPlayerSpawnSystem> logger
    ) {
        _transport = transport;
        _playerBuilder = playerBuilder;
        _logger = logger;
    }

    public void RequestSpawn(Character character)
    {
        _requests.Enqueue(new PlayerSpawnRequest(character));
    }

    public void Tick(float delta, DataStore store)
    {
        if (!_spawned && _pending == null && _requests.TryDequeue(out PlayerSpawnRequest request))
        {
            _pending = request;
            _sent = false;
        }

        if (!_spawned && !_sent && _pending != null)
        {
            _transport.Send(new SpawnRequest { CharacterId = _pending.Value.Character.Id });
            _sent = true;
        }

        Result<SpawnResponse> receiveResult;
        while ((receiveResult = _transport.Receive<SpawnResponse>()).Success)
        {
            SpawnResponse response = receiveResult.Value;
            if (_spawned || !response.Accepted || _pending == null)
            {
                continue;
            }

            PlayerSpawnRequest spawnRequest = _pending.Value;

            Uuid playerUuid = Uuid.FromValue(response.Entity);
            if (!store.TryGet(playerUuid, out int entity))
            {
                entity = store.Alloc(playerUuid);
            }

            _playerBuilder.Decorate(new Entity(entity, store), spawnRequest.Character);

            _spawned = true;
            _sent = false;
            _pending = null;

            _logger.LogInformation("Spawned local player entity {uuid}.", playerUuid);
        }
    }

    private readonly struct PlayerSpawnRequest
    {
        public readonly Character Character;

        public PlayerSpawnRequest(Character character)
        {
            Character = character;
        }
    }
}