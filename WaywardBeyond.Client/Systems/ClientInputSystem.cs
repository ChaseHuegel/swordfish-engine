using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Networking;

namespace WaywardBeyond.Client.Systems;

internal sealed class ClientInputSystem : IEntitySystem
{
    private const float MOUSE_SENSITIVITY = 0.001f;
    private const float ROLL_RATE = 10f;

    //  An idle player samples the same command every tick. The server's staged command stays active
    //  until a newer sample supersedes it, so resending the identical sample only wastes upload
    //  bandwidth. A slow keepalive keeps the ack and lag bookkeeping fresh.
    private const float IDLE_KEEPALIVE_SECONDS = 1f;

    private readonly ClientPlayerMotionProcessor _motionProcessor;
    private readonly IInputService _inputService;
    private readonly ControlSettings _controlSettings;
    private readonly SnapshotAckTracker _snapshotAck;

    private uint _sequenceNumber;
    private InputComponent _lastSent;
    private bool _hasSent;
    private float _sinceSend;

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
        _sinceSend += delta;

        bool inputEnabled = _motionProcessor.IsInputEnabled;
        Vector3 movement = inputEnabled ? GetMovementInput() : Vector3.Zero;

        float sensitivityModifier = _controlSettings.LookSensitivity / 5f;
        while (_motionProcessor.CursorUpdates.TryDequeue(out Vector2 cursorDelta))
        {
            if (!inputEnabled)
            {
                continue;
            }

            _lookYaw += -cursorDelta.X * MOUSE_SENSITIVITY * sensitivityModifier;
            _lookPitch += -cursorDelta.Y * MOUSE_SENSITIVITY * sensitivityModifier;
        }

        float rollDirection = inputEnabled ? (_inputService.IsKeyHeld(Key.Q) ? 1f : 0f) - (_inputService.IsKeyHeld(Key.E) ? 1f : 0f) : 0f;
        _lookRoll += rollDirection * ROLL_RATE * MathS.DEGREES_TO_RADIANS * delta;

        bool primaryHeld = _inputService.IsMouseHeld(MouseButton.Left);
        bool secondaryHeld = _inputService.IsMouseHeld(MouseButton.Right);

        var input = new InputComponent
        {
            MovementX = movement.X,
            MovementY = movement.Y,
            MovementZ = movement.Z,
            LookPitch = _lookPitch,
            LookYaw = _lookYaw,
            LookRoll = _lookRoll,
            ServerTickAtSample = _snapshotAck.LastAppliedSnapshotTick,
            PrimaryHeld = inputEnabled && primaryHeld,
            SecondaryHeld = inputEnabled && secondaryHeld,
        };

        //  The first sample after construction is always written. Afterwards an identical sample is
        //  skipped unless the keepalive interval elapsed. The server and the local pending ring keep
        //  applying the newest staged command, so held keys and idle damping continue unchanged.
        if (_hasSent && IsSameSample(in input, in _lastSent) && _sinceSend < IDLE_KEEPALIVE_SECONDS)
        {
            return;
        }

        input.SequenceNumber = ++_sequenceNumber;
        _lastSent = input;
        _hasSent = true;
        _sinceSend = 0f;

        CollectInputAction collectInput = new() { Input = input };
        store.Query<PlayerComponent, CollectInputAction>(0f, ref collectInput);
    }

    /// <summary>Clears look totals, the last sample, and the step's look history for a new join session.</summary>
    internal void ResetSession(Uuid playerUuid)
    {
        _sequenceNumber = 0;
        _lastSent = default;
        _hasSent = false;
        _sinceSend = 0f;
        _lookPitch = 0f;
        _lookYaw = 0f;
        _lookRoll = 0f;
        _motionProcessor.CursorUpdates.Clear();
        _motionProcessor.Step?.ForgetLookHistory(playerUuid);
    }

    /// <summary>
    /// Compares the fields a consumer acts on. The sequence number and sampled server tick are
    /// bookkeeping and always differ, so they must not force a new upload.
    /// </summary>
    private static bool IsSameSample(in InputComponent a, in InputComponent b)
    {
        return a.MovementX == b.MovementX
            && a.MovementY == b.MovementY
            && a.MovementZ == b.MovementZ
            && a.LookPitch == b.LookPitch
            && a.LookYaw == b.LookYaw
            && a.LookRoll == b.LookRoll
            && a.PrimaryHeld == b.PrimaryHeld
            && a.SecondaryHeld == b.SecondaryHeld;
    }

    /// <summary>
    /// Latch the sampled command onto the local player and push it into the pending ring buffer for
    /// replay after an authoritative snapshot.
    /// </summary>
    private struct CollectInputAction : IForEach<PlayerComponent>
    {
        public InputComponent Input;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player)
        {
            store.AddOrUpdate(entity, Input);

            if (!store.TryGet(entity, out PendingInputComponent pending) || pending.Ring == null)
            {
                pending = PendingInputComponent.Create();
                store.AddOrUpdate(entity, pending);
            }

            pending.Ring.Add(Input);
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
