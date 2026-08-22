using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Networking;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Snapshots;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class ClientInputSystem : IEntitySystem
{
    private readonly IInputService _inputService;
    private readonly INetworkTransport _transport;
    private readonly SnapshotAckTracker _snapshotAck;

    private uint _sequenceNumber;

    public ClientInputSystem(
        in IInputService inputService,
        in INetworkTransport transport,
        SnapshotAckTracker snapshotAck
    ) {
        _inputService = inputService;
        _transport = transport;
        _snapshotAck = snapshotAck;
    }

    public void Tick(float delta, DataStore store)
    {
        if (!_transport.IsConnected)
        {
            return;
        }

        Vector3 movement = GetMovementInput();
        Vector2 lookDelta = _inputService.CursorDelta;
        bool jump = _inputService.IsKeyHeld(Key.Space);

        var input = new InputComponent
        {
            Movement = movement,
            LookDelta = lookDelta,
            Jump = jump,
            SequenceNumber = ++_sequenceNumber,
            ServerTickAtSample = _snapshotAck.LastAppliedSnapshotTick,
        };

        Uuid clientUuid = Uuid.Null;
        CollectInputAction collectInput = new() { Input = input, ClientUuid = clientUuid };
        store.Query<PlayerComponent, CollectInputAction>(0f, ref collectInput);
        clientUuid = collectInput.ClientUuid;

        ClientInputMsg msg = input.ToMessage(0, clientUuid);
        _transport.Send(msg);
    }

    private struct CollectInputAction : IForEach<PlayerComponent>
    {
        public InputComponent Input;
        public Uuid ClientUuid;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent cleanupAudioPlayer)
        {
            store.AddOrUpdate(entity, Input);

            ClientUuid = store.GetUuid(entity);

            if (!store.TryGet(entity, out PendingInputComponent pending))
            {
                pending = new PendingInputComponent();
            }

            pending.Push(Input);
            store.AddOrUpdate(entity, pending);
        }
    }

    private Vector3 GetMovementInput()
    {
        var movement = new Vector3();

        if (_inputService.IsKeyHeld(Key.W))
        {
            
            movement -= Vector3.UnitZ;
        }
        
        if (_inputService.IsKeyHeld(Key.S))
        {
            movement += Vector3.UnitZ;
        }
        
        if (_inputService.IsKeyHeld(Key.D))
        {
            movement += Vector3.UnitX;
        }
        
        if (_inputService.IsKeyHeld(Key.A))
        {
            movement -= Vector3.UnitX;
        }
        if (_inputService.IsKeyHeld(Key.Space))
        {
            movement += Vector3.UnitY;
        }
        
        if (_inputService.IsKeyHeld(Key.Control))
        {
            movement -= Vector3.UnitY;
        }

        return movement;
    }
}
