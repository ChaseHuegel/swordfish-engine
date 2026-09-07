using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Networking;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class ClientInputSystem : IEntitySystem
{
    private const float MOUSE_SENSITIVITY = 0.01f;
    private const float ROLL_RATE = 50f;

    private readonly ClientPlayerMotionProcessor _motionProcessor;
    private readonly IInputService _inputService;
    private readonly ControlSettings _controlSettings;
    private readonly SnapshotAckTracker _snapshotAck;

    private uint _sequenceNumber;

    //  Running absolute look totals in radians. The wire carries these totals and the shared step applies
    //  the per-sim-tick difference, so no input is dropped regardless of sampling vs physics cadence.
    //  Sensitivity and roll rate stay client-local.
    private float _lookPitch;
    private float _lookYaw;
    private float _lookRoll;

    public ClientInputSystem(
        in ClientPlayerMotionProcessor motionProcessor,
        in IInputService inputService,
        in ControlSettings controlSettings,
        SnapshotAckTracker snapshotAck
    ) {
        _motionProcessor = motionProcessor;
        _inputService = inputService;
        _controlSettings = controlSettings;
        _snapshotAck = snapshotAck;
    }

    public void Tick(float delta, DataStore store)
    {
        Vector3 movement = GetMovementInput();
        bool jump = _inputService.IsKeyHeld(Key.Space);

        //  Mouse sensitivity is a client-local setting. Resolve the captured cursor deltas (window path)
        //  and Q/E roll into accumulated radians here so the wire only ever carries resolved look totals.
        float sensitivityModifier = _controlSettings.LookSensitivity / 5f;
        while (_motionProcessor.CursorUpdates.TryDequeue(out Vector2 cursorDelta))
        {
            _lookYaw += -cursorDelta.X * MOUSE_SENSITIVITY * sensitivityModifier;
            _lookPitch += -cursorDelta.Y * MOUSE_SENSITIVITY * sensitivityModifier;
        }

        float rollDirection = (_inputService.IsKeyHeld(Key.Q) ? 1f : 0f) - (_inputService.IsKeyHeld(Key.E) ? 1f : 0f);
        _lookRoll += rollDirection * ROLL_RATE * MathS.DEGREES_TO_RADIANS * delta;

        var input = new InputComponent
        {
            MovementX = movement.X,
            MovementY = movement.Y,
            MovementZ = movement.Z,
            LookPitch = _lookPitch,
            LookYaw = _lookYaw,
            LookRoll = _lookRoll,
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
