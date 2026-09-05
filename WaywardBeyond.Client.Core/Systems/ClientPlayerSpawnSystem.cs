using System.Numerics;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Voxels.Building;
using WaywardBeyond.Client.Core.Voxels.Models;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Client-side adjacent to server-authoritative spawn. Submits a <see cref="SpawnRequest"/> on
/// behalf of a local player, and once the server assigns an entity uuid it materializes the local
/// (client-owned) player onto the client world, keyed to the server's identity.
/// </summary>
internal sealed class ClientPlayerSpawnSystem : IEntitySystem
{
    private readonly IClientConnection _transport;
    private readonly PlayerCharacterEntityBuilder _playerBuilder;
    private readonly ILogger<ClientPlayerSpawnSystem> _logger;

    private Character? _character;
    private CharacterEntityModel? _model;
    private bool _requested;
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
        _character = character;
        _model = model;
        _requested = true;
        _spawned = false;
    }

    public void Tick(float delta, DataStore store)
    {
        if (_requested && !_spawned && _model is { } model)
        {
            var request = new SpawnRequest
            {
                CharacterId = _character?.Id ?? 0,
                PositionX = model.Position.X,
                PositionY = model.Position.Y,
                PositionZ = model.Position.Z,
                OrientationX = model.Orientation.X,
                OrientationY = model.Orientation.Y,
                OrientationZ = model.Orientation.Z,
                OrientationW = model.Orientation.W,
            };
            _transport.Send(request);
        }

        Result<SpawnResponse> receiveResult;
        while ((receiveResult = _transport.Receive<SpawnResponse>()).Success)
        {
            SpawnResponse response = receiveResult.Value;
            if (_spawned || !response.Accepted || _character is not { } character || _model is not { } spawnModel)
            {
                continue;
            }

            Uuid playerUuid = Uuid.FromValue(response.Entity);
            if (!store.TryGet(playerUuid, out int entity))
            {
                entity = store.Alloc(playerUuid);
            }

            _playerBuilder.Decorate(new Entity(entity, store), character, spawnModel);
            _spawned = true;
            _requested = false;
            _logger.LogInformation("Spawned local player entity {uuid}.", playerUuid);
        }
    }
}