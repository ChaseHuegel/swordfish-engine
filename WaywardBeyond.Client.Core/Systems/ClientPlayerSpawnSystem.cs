using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Voxels.Building;
using WaywardBeyond.Client.Core.Voxels.Models;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Submits a <see cref="SpawnRequest"/> on behalf of a local player and, once the server assigns an
/// entity uuid, materializes the local player and hands its initial transform up as a placement so
/// the server mirror starts at the right position. Requests are handed off over a queue so the load
/// thread never mutates state the ECS thread reads; each request is sent at most once.
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

    public void RequestSpawn(Character character, CharacterEntityModel model)
    {
        _requests.Enqueue(new PlayerSpawnRequest(character, model));
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

            Character character = _pending.Value.Character;
            CharacterEntityModel model = _pending.Value.Model;

            Uuid playerUuid = Uuid.FromValue(response.Entity);
            if (!store.TryGet(playerUuid, out int entity))
            {
                entity = store.Alloc(playerUuid);
            }

            _playerBuilder.Decorate(new Entity(entity, store), character, model);
            SendInitialTransform(store, entity);

            _spawned = true;
            _sent = false;
            _pending = null;

            _logger.LogInformation("Spawned local player entity {uuid}.", playerUuid);
        }
    }

    private void SendInitialTransform(DataStore store, int entity)
    {
        if (!NetworkRegistry.TryGetInfo(typeof(TransformComponent), out NetworkComponentInfo info)
            || !store.TryGet<TransformComponent>(entity, out _))
        {
            return;
        }

        byte[] payload = info.Codec.Serialize(store, entity);
        var placement = new WorldSnapshot
        {
            Components = [new ComponentSnapshot(store.GetUuid(entity).ToValue(), info.Uuid.ToValue(), payload)],
            RemovedEntities = [],
        };

        _transport.Send(placement);
    }

    private readonly struct PlayerSpawnRequest
    {
        public readonly Character Character;
        public readonly CharacterEntityModel Model;

        public PlayerSpawnRequest(Character character, CharacterEntityModel model)
        {
            Character = character;
            Model = model;
        }
    }
}