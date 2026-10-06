using System;
using System.Collections.Concurrent;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Client.Core.Voxels.Building;
using WaywardBeyond.Shared.Bricks;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Runs the client side of the join handshake and full-world stream, driving
/// <c>MainMenu → Loading → Playing</c>. The menu enqueues a join request; on the ECS thread the system
/// sends a <see cref="JoinRequest"/>, courts the <see cref="JoinAccept"/>, builds view entities as each
/// <see cref="LevelEntityAdd"/> arrives, and only transitions to <c>Playing</c> once the server's
/// <see cref="LevelStreamComplete"/> lands. Because <see cref="ClientReconcileSystem"/> gates on
/// <c>Playing</c>, authoritative snapshots stay inert until the world is fully streamed - the join-time
/// ordering guard. View entities are built here (on the ECS thread), never the menu/UI thread.
/// </summary>
internal sealed class ClientJoinSystem : IEntitySystem
{
    private readonly IClientConnection _transport;
    private readonly PlayerCharacterEntityBuilder _playerBuilder;
    private readonly VoxelEntityBuilder _voxelBuilder;
    private readonly IBrickIdMap _brickIdMap;
    private readonly ClientDisconnectSystem _disconnectSystem;
    private readonly NetworkingSettings _settings;
    private readonly ILogger<ClientJoinSystem> _logger;

    private readonly ConcurrentQueue<JoinRequestData> _requests = new();

    private JoinRequestData? _request;
    private bool _sent;
    private bool _seated;
    private int _joinStartedTicks;

    public ClientJoinSystem(
        in IClientConnection transport,
        in PlayerCharacterEntityBuilder playerBuilder,
        in VoxelEntityBuilder voxelBuilder,
        ILogger<ClientJoinSystem> logger,
        IBrickIdMap brickIdMap,
        in ClientDisconnectSystem disconnectSystem,
        in NetworkingSettings settings
    ) {
        _transport = transport;
        _playerBuilder = playerBuilder;
        _voxelBuilder = voxelBuilder;
        _brickIdMap = brickIdMap;
        _disconnectSystem = disconnectSystem;
        _settings = settings;
        _logger = logger;
    }

    public void RequestJoin(in Character character, string levelGuid)
    {
        _requests.Enqueue(new JoinRequestData(character, levelGuid));
    }

    public void Tick(float delta, DataStore store)
    {
        if (_request == null && _requests.TryDequeue(out JoinRequestData request))
        {
            _request = request;
            _sent = false;
            _seated = false;
        }

        if (_request != null && !_sent)
        {
            _joinStartedTicks = Environment.TickCount;
            JoinRequestData pending = _request.Value;
            Character character = pending.Character;
            Result send = _transport.Send(new JoinRequest
            {
                LevelGuid = pending.LevelGuid,
                CharacterId = character.Id,
                PublicView = new PublicView
                {
                    CharacterId = character.Id,
                    Name = character.Name,
                    Body = character.Body,
                },
                Seed = new CharacterSeed
                {
                    CharacterId = character.Id,
                    Name = character.Name,
                    Body = character.Body,
                    //  Null for a client-authored new character: the server grants the starter loadout.
                    //  A saved inventory is sent as-is and is never restocked server-side.
                    InventoryContents = character.Inventory,
                    ActiveInventorySlot = character.ActiveInventorySlot,
                    GameMode = (int)character.GameMode,
                    //  The client owns the initial skill seed: its saved statistics seed the server's
                    //  transient per-session skill state, which the server then owns for the session.
                    Statistics = character.Statistics,
                },
            });
            if (!send.Success)
            {
                //  No active connection: fail fast with the connection-lost teardown instead of sitting
                //  in Loading until the join-stream timeout.
                _logger.LogWarning("Failed to send join request: {message}.", send.Message);
                _request = null;
                _sent = false;
                _disconnectSystem.RequestDisconnect();
                return;
            }
            _sent = true;
        }

        Result<JoinAccept> acceptResult;
        while ((acceptResult = _transport.Receive<JoinAccept>()).Success)
        {
            JoinAccept accept = acceptResult.Value;
            if (_request != null && !_seated)
            {
                SeatPlayer(accept, _request.Value.Character, store, out _seated);
            }
        }

        Result<LevelEntityAdd> addResult;
        while ((addResult = _transport.Receive<LevelEntityAdd>()).Success)
        {
            BuildViewEntity(addResult.Value, store);
        }

        Result<LevelStreamComplete> completeResult;
        while ((completeResult = _transport.Receive<LevelStreamComplete>()).Success)
        {
            WaywardBeyond.GameState.Set(GameState.Playing);
            _request = null;
            _sent = false;
            _logger.LogInformation("World stream complete; beginning play.");
        }

        //  A join that never completes (undelivered LevelStreamComplete, dying link, or a world too
        //  large to drain) must not stall Loading forever: abort and return to the menu with the
        //  connection-lost notice.
        if (_request != null && _sent && Environment.TickCount - _joinStartedTicks > _settings.JoinStreamTimeoutMs.Get())
        {
            _logger.LogWarning("Join timed out after {timeoutMs} ms awaiting the world stream.", _settings.JoinStreamTimeoutMs.Get());
            _request = null;
            _sent = false;
            _disconnectSystem.RequestDisconnect();
        }
    }

    private void SeatPlayer(in JoinAccept accept, in Character character, DataStore store, out bool seated)
    {
        Uuid playerUuid = Uuid.FromValue(accept.PlayerEntity);
        if (playerUuid == Uuid.Null)
        {
            seated = false;
            return;
        }

        int entity = store.TryGet(playerUuid, out int existing) ? existing : store.Alloc(playerUuid);
        _playerBuilder.Decorate(new Entity(entity, store), character);
        seated = true;
        _logger.LogInformation("Seated local player entity {uuid}.", playerUuid);
    }

    private void BuildViewEntity(in LevelEntityAdd add, DataStore store)
    {
        VoxelEntityData data = add.VoxelEntity;
        if (data.Uuid == 0 || data.Chunks == null || data.Chunks.Length == 0)
        {
            return;
        }

        byte chunkSize = data.Chunks[0].Chunk.Size;
        if (chunkSize <= 0 || (chunkSize & (chunkSize - 1)) != 0)
        {
            chunkSize = 16;
        }

        //  Resolve the server's palette-indexed voxel ids into this process's local id space.
        VoxelEntityData local = VoxelEntityDataCodec.DecodeToLocal(data, _brickIdMap);
        var voxelObject = new VoxelObject(chunkSize, local.Chunks);
        _voxelBuilder.Create(
            store,
            Uuid.FromValue(data.Uuid),
            voxelObject,
            new Vector3((float)data.X, (float)data.Y, (float)data.Z),
            new Quaternion(data.OrientationX, data.OrientationY, data.OrientationZ, data.OrientationW),
            new Vector3(data.ScaleX, data.ScaleY, data.ScaleZ)
        );
    }

    private readonly struct JoinRequestData
    {
        public readonly Character Character;
        public readonly string LevelGuid;

        public JoinRequestData(in Character character, string levelGuid)
        {
            Character = character;
            LevelGuid = levelGuid;
        }
    }
}