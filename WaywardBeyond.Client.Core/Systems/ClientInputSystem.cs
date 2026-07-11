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

        uint clientId = 0;
        store.Query<PlayerComponent>(0f, (float d, DataStore s, int e, ref PlayerComponent player) =>
        {
            s.AddOrUpdate(e, input);

            if (s.TryGet(e, out NetworkComponent net))
            {
                clientId = net.NetworkID;
            }

            if (!s.TryGet(e, out PendingInputComponent pending))
            {
                pending = new PendingInputComponent();
            }

            pending.Push(input);
            s.AddOrUpdate(e, pending);
        });

        ClientInputMsg msg = input.ToMessage(0, clientId);
        _transport.Send(msg);
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
