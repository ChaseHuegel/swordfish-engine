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
    private const float MOUSE_SENSITIVITY = 0.001f;
    private const float ROLL_RATE = 10f;

    private readonly ClientPlayerMotionProcessor _motionProcessor;
    private readonly IInputService _inputService;
    private readonly ControlSettings _controlSettings;
    private readonly SnapshotAckTracker _snapshotAck;

    private uint _sequenceNumber;
    private uint _interactionSequence;

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
        CollectContextAction collectContext = new() { Enabled = inputEnabled };
        store.Query<PlayerComponent, EquipmentComponent, CollectContextAction>(0f, ref collectContext);

        var input = new InputComponent
        {
            MovementX = movement.X,
            MovementY = movement.Y,
            MovementZ = movement.Z,
            LookPitch = _lookPitch,
            LookYaw = _lookYaw,
            LookRoll = _lookRoll,
            SequenceNumber = ++_sequenceNumber,
            ServerTickAtSample = _snapshotAck.LastAppliedSnapshotTick,
            HeldSlot = inputEnabled ? collectContext.HeldSlot : 0,
            PrimaryHeld = inputEnabled && primaryHeld,
            SecondaryHeld = inputEnabled && secondaryHeld,
        };

        CollectInputAction collectInput = new() { Input = input };
        store.Query<PlayerComponent, CollectInputAction>(0f, ref collectInput);

        InteractionKind edge = GetInteractionEdge(inputEnabled);
        if (edge != InteractionKind.None)
        {
            var interaction = new InteractionEvent
            {
                SequenceNumber = ++_interactionSequence,
                ServerTickAtSample = input.ServerTickAtSample,
                Kind = (byte)edge,
                Brick = null,
            };
            CollectInteractionAction collectInteraction = new() { Interaction = interaction };
            store.Query<PlayerComponent, CollectInteractionAction>(0f, ref collectInteraction);
        }
    }

    private InteractionKind GetInteractionEdge(bool inputEnabled)
    {
        if (!inputEnabled)
        {
            return InteractionKind.None;
        }

        //  Press edges win over released edges of the same gap so a quick click forwards as the action.
        if (_inputService.IsMousePressed(MouseButton.Left))
        {
            return InteractionKind.PrimaryPressed;
        }
        if (_inputService.IsMousePressed(MouseButton.Right))
        {
            return InteractionKind.SecondaryPressed;
        }
        if (_inputService.IsMouseReleased(MouseButton.Left))
        {
            return InteractionKind.PrimaryReleased;
        }
        if (_inputService.IsMouseReleased(MouseButton.Right))
        {
            return InteractionKind.SecondaryReleased;
        }
        return InteractionKind.None;
    }

    private struct CollectContextAction : IForEach<PlayerComponent, EquipmentComponent>
    {
        public bool Enabled;

        public uint HeldSlot;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player, in EquipmentComponent equipment)
        {
            if (Enabled)
            {
                HeldSlot = (uint)equipment.ActiveInventorySlot;
            }
        }
    }

    /// <summary>
    /// Latch a discrete interaction edge onto the local player entity. The component stays dirty until
    /// <see cref="ClientReplicationSystem"/> publishes it, so a tap landing in a throttled send gap is
    /// still delivered on the next packet — lossless, latency only.
    /// </summary>
    private struct CollectInteractionAction : IForEach<PlayerComponent>
    {
        public InteractionEvent Interaction;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player)
        {
            InteractionEvent interaction = Interaction;
            interaction.Entity = store.GetUuid(entity).ToValue();
            store.AddOrUpdate(entity, interaction);
        }
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
