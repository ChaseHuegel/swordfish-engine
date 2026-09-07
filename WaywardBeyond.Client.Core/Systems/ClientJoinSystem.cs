using System.Collections.Concurrent;
using System.Numerics;
using DryIoc;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Client.Core.Voxels.Building;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Runs the client side of the join handshake and full-world stream, driving
/// <c>MainMenu → Loading → Playing</c>. The menu enqueues a join request; on the ECS thread the system
/// sends a <see cref="JoinRequest"/>, courts the <see cref="JoinAccept"/>, builds view entities as each
/// <see cref="WorldEntityAdd"/> arrives, and only transitions to <c>Playing</c> once the server's
/// <see cref="WorldStreamComplete"/> lands. Because <see cref="ClientReconcileSystem"/> gates on
/// <c>Playing</c>, authoritative snapshots stay inert until the world is fully streamed - the join-time
/// ordering guard. View entities are built here (on the ECS thread), never the menu/UI thread.
/// </summary>
internal sealed class ClientJoinSystem : IEntitySystem
{
    private readonly IClientConnection _transport;
    private readonly PlayerCharacterEntityBuilder _playerBuilder;
    private readonly IContainer _container;
    private readonly ILogger<ClientJoinSystem> _logger;

    //  VoxelEntityBuilder is render-coupled and transitively depends on the client DataStore, which is
    //  resolved from the ECSContext being constructed during boot. Injecting it here would recurse
    //  through DataStore -> IECSContext -> IEntitySystem[] during container build. It is resolved lazily
    //  on the ECS thread at the first WorldEntityAdd, by which point the ECS world is fully constructed.
    private VoxelEntityBuilder? _voxelBuilder;

    private readonly ConcurrentQueue<JoinRequestData> _requests = new();

    private JoinRequestData? _request;
    private bool _sent;
    private bool _seated;

    public ClientJoinSystem(
        in IClientConnection transport,
        in PlayerCharacterEntityBuilder playerBuilder,
        in IContainer container,
        ILogger<ClientJoinSystem> logger
    ) {
        _transport = transport;
        _playerBuilder = playerBuilder;
        _container = container;
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
            JoinRequestData pending = _request.Value;
            _transport.Send(new JoinRequest
            {
                LevelGuid = pending.LevelGuid,
                CharacterId = pending.Character.Id,
                PublicView = new PublicView
                {
                    CharacterId = pending.Character.Id,
                    Name = pending.Character.Name,
                    Body = pending.Character.Body,
                },
            });
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

        Result<WorldEntityAdd> addResult;
        while ((addResult = _transport.Receive<WorldEntityAdd>()).Success)
        {
            BuildViewEntity(addResult.Value, store);
        }

        Result<WorldStreamComplete> completeResult;
        while ((completeResult = _transport.Receive<WorldStreamComplete>()).Success)
        {
            WaywardBeyond.GameState.Set(GameState.Playing);
            _request = null;
            _sent = false;
            _logger.LogInformation("World stream complete; beginning play.");
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

    private void BuildViewEntity(in WorldEntityAdd add, DataStore store)
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

        var voxelObject = new VoxelObject(chunkSize, data.Chunks);
        VoxelEntityBuilder voxelBuilder = _voxelBuilder ??= _container.Resolve<VoxelEntityBuilder>();
        voxelBuilder.Create(
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