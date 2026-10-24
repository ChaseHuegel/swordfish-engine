using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NsdRpc;
using Swordfish.ECS;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Runs the client side of the join handshake and world stream, driving
/// <c>MainMenu → Loading → Playing</c>. The join is a correlated RPC call. On success the system starts
/// the level stream call with the inbound queue as its chunk sink, reassembles each entity on the ECS
/// thread, and enters <c>Playing</c> only after the stream response lands and every chunk applied.
/// </summary>
internal sealed class ClientJoinSystem : IEntitySystem
{
    private readonly ClientRpcHost _host;
    private readonly ClientInbound _inbound;
    private readonly PlayerCharacterEntityBuilder _playerBuilder;
    private readonly VoxelEntityBuilder _voxelBuilder;
    private readonly IBrickRegistry _brickRegistry;
    private readonly ClientDisconnectSystem _disconnectSystem;
    private readonly NetworkingConfig _config;
    private readonly ILogger<ClientJoinSystem> _logger;

    private readonly ConcurrentQueue<JoinRequestData> _requests = new();
    private readonly FragmentAssembler _levelAssembler = new();

    private JoinRequestData? _request;
    private Task<RpcResult<JoinAccept>>? _joinTask;
    private Task<RpcResult<LevelStreamComplete>>? _streamTask;
    private CancellationTokenSource? _timeout;
    private bool _seated;

    public ClientJoinSystem(
        in ClientRpcHost host,
        in ClientInbound inbound,
        in PlayerCharacterEntityBuilder playerBuilder,
        in VoxelEntityBuilder voxelBuilder,
        ILogger<ClientJoinSystem> logger,
        IBrickRegistry brickRegistry,
        in ClientDisconnectSystem disconnectSystem,
        in NetworkingConfig config
    ) {
        _host = host;
        _inbound = inbound;
        _playerBuilder = playerBuilder;
        _voxelBuilder = voxelBuilder;
        _brickRegistry = brickRegistry;
        _disconnectSystem = disconnectSystem;
        _config = config;
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
            BeginJoin(request);
        }

        if (_request == null)
        {
            return;
        }

        PollJoin(store);
        PollStream(store);
    }

    private void BeginJoin(in JoinRequestData join)
    {
        _request = join;
        _seated = false;
        _joinTask = null;
        _streamTask = null;
        _levelAssembler.Reset();
        while (_inbound.LevelChunks.TryDequeue(out _)) { }
        _timeout?.Dispose();
        _timeout = new CancellationTokenSource(Math.Max(1, _config.Protocol.JoinStreamTimeoutMs.Get()));

        GameServerApiClient? api = _host.Api;
        if (api == null)
        {
            _logger.LogWarning("Failed to start a join: no active connection.");
            FailJoin();
            return;
        }

        Character character = join.Character;
        _joinTask = api.JoinAsync(new JoinRequest
        {
            LevelGuid = join.LevelGuid,
            CharacterUuid = character.Uuid,
            View = new CharacterView
            {
                CharacterUuid = character.Uuid,
                Name = character.Name,
                Body = character.Body,
            },
            State = new CharacterState
            {
                CharacterUuid = character.Uuid,
                Name = character.Name,
                Body = character.Body,
                Inventory = character.Inventory,
                ActiveInventorySlot = character.ActiveInventorySlot,
                GameMode = character.GameMode,
                Statistics = character.Statistics,
            },
        }, _timeout.Token);
    }

    private void PollJoin(DataStore store)
    {
        if (_joinTask == null || !_joinTask.IsCompleted)
        {
            CheckTimeout();
            return;
        }

        RpcResult<JoinAccept> result = _joinTask.GetAwaiter().GetResult();
        _joinTask = null;

        if (!result.IsSuccess || _request == null)
        {
            _logger.LogWarning("Join failed: {message}.", result.ErrorMessage);
            FailJoin();
            return;
        }

        if (!_seated)
        {
            SeatPlayer(result.Value, _request.Value.Character, store);
        }

        GameServerApiClient? api = _host.Api;
        if (api == null)
        {
            FailJoin();
            return;
        }

        _logger.LogInformation("Join accepted; streaming the world.");
        _streamTask = api.StreamLevelAsync(
            new StreamLevelRequest { LevelGuid = _request.Value.LevelGuid },
            _inbound,
            _timeout!.Token);
    }

    private void PollStream(DataStore store)
    {
        DrainLevelChunks(store);
        if (_request == null)
        {
            return;
        }

        if (_streamTask == null || !_streamTask.IsCompleted)
        {
            CheckTimeout();
            return;
        }

        RpcResult<LevelStreamComplete> result = _streamTask.GetAwaiter().GetResult();
        _streamTask = null;

        //  The response follows every chunk on the ordered transport, but drain once more so a chunk
        //  handed to the queue by the receive pump is never left behind.
        DrainLevelChunks(store);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("World stream failed: {message}.", result.ErrorMessage);
            FailJoin();
            return;
        }

        if (_levelAssembler.IsActive)
        {
            _logger.LogWarning("World stream ended with an incomplete entity.");
            FailJoin();
            return;
        }

        WaywardBeyond.GameState.Set(GameState.Playing);
        _logger.LogInformation("World stream complete; beginning play.");
        ResetJoin();
    }

    private void DrainLevelChunks(DataStore store)
    {
        while (_inbound.LevelChunks.TryDequeue(out LevelStreamChunk chunk))
        {
            if (chunk.PartCount == 0 || chunk.PartIndex >= chunk.PartCount)
            {
                _logger.LogWarning("World stream part {index}/{count} is invalid.", chunk.PartIndex, chunk.PartCount);
                FailJoin();
                return;
            }

            FragmentState state = _levelAssembler.Append(
                groupId: 0,
                partIndex: (int)chunk.PartIndex,
                partCount: (int)chunk.PartCount,
                chunk.Payload,
                out byte[] payload);

            if (state == FragmentState.Complete)
            {
                BuildViewEntity(VoxelEntityData.Deserialize(payload), store);
                continue;
            }

            //  The stream is ordered on the wire, so a rejected part means the join is corrupt.
            if (state == FragmentState.Rejected)
            {
                _logger.LogWarning("World stream part {index}/{count} was rejected.", chunk.PartIndex, chunk.PartCount);
                FailJoin();
                return;
            }
        }
    }

    private void CheckTimeout()
    {
        if (_timeout != null && !_timeout.IsCancellationRequested)
        {
            return;
        }

        _logger.LogWarning("Join timed out after {timeoutMs} ms.", _config.Protocol.JoinStreamTimeoutMs.Get());
        FailJoin();
    }

    private void FailJoin()
    {
        _host.Api?.LeaveGame(new LeaveGameRequest());
        while (_inbound.LevelChunks.TryDequeue(out _)) { }
        ResetJoin();
        _disconnectSystem.RequestDisconnect();
    }

    private void ResetJoin()
    {
        _request = null;
        _joinTask = null;
        _streamTask = null;
        _seated = false;
        _levelAssembler.Reset();
        _timeout?.Dispose();
        _timeout = null;
    }

    private void SeatPlayer(in JoinAccept accept, in Character character, DataStore store)
    {
        Uuid playerUuid = Uuid.FromValue(accept.PlayerEntity);
        if (playerUuid == Uuid.Null)
        {
            return;
        }

        int entity = store.TryGet(playerUuid, out int existing) ? existing : store.Alloc(playerUuid);
        _playerBuilder.Decorate(new Entity(entity, store), character);
        _seated = true;
        _logger.LogInformation("Seated local player entity {uuid}.", playerUuid);
    }

    private void BuildViewEntity(in VoxelEntityData data, DataStore store)
    {
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
        VoxelEntityData local = VoxelCodec.Decode(data, _brickRegistry);

        VoxelObject voxelObject;
        try
        {
            voxelObject = new VoxelObject(chunkSize, local.Chunks);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Skipped streamed voxel entity {uuid} with malformed chunk data.", data.Uuid);
            return;
        }

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
