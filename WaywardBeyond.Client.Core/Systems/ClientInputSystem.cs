using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Networking;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class ClientInputSystem : IEntitySystem
{
    private const float MOUSE_SENSITIVITY = 0.1f;

    private readonly IInputService _inputService;
    private readonly ControlSettings _controlSettings;
    private readonly SnapshotAckTracker _snapshotAck;

    private uint _sequenceNumber;
    private float _yaw;
    private float _pitch;

    public ClientInputSystem(
        in IInputService inputService,
        in ControlSettings controlSettings,
        SnapshotAckTracker snapshotAck
    ) {
        _inputService = inputService;
        _controlSettings = controlSettings;
        _snapshotAck = snapshotAck;
    }

    public void Tick(float delta, DataStore store)
    {
        Vector3 movement = GetMovementInput();
        Vector2 cursorDelta = _inputService.CursorDelta;
        bool jump = _inputService.IsKeyHeld(Key.Space);

        //  Mouse sensitivity is a client-local setting. Resolve it against the raw cursor delta into
        //  absolute yaw/pitch here so the wire only ever carries resolved radians.
        float sensitivityModifier = _controlSettings.LookSensitivity / 5f;
        _yaw += -cursorDelta.X * MOUSE_SENSITIVITY * sensitivityModifier;
        _pitch += -cursorDelta.Y * MOUSE_SENSITIVITY * sensitivityModifier;

        var input = new InputComponent
        {
            MovementX = movement.X,
            MovementY = movement.Y,
            MovementZ = movement.Z,
            LookYaw = _yaw,
            LookPitch = _pitch,
            Jump = jump,
            SequenceNumber = ++_sequenceNumber,
            ServerTickAtSample = _snapshotAck.LastAppliedSnapshotTick,
        };

        CollectInputAction collectInput = new() { Input = input };
        store.Query<PlayerComponent, CollectInputAction>(0f, ref collectInput);
    }

    private struct CollectInputAction : IForEach<PlayerComponent>
    {
        public InputComponent Input;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player)
        {
            store.AddOrUpdate(entity, Input);

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
