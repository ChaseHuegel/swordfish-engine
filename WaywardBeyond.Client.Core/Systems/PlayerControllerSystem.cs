using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Events;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class PlayerControllerSystem : IEntitySystem
{
    private const float MOUSE_SENSITIVITY = 0.1f;
    private const float BASE_SPEED = 10;
    private const float ROLL_RATE = 50;
    private const float DECELERATION = 2f;
    private const float ANGULAR_DECELERATION = 50f;

    private readonly IInputService _inputService;
    private readonly ControlSettings _controlSettings;
    private readonly EventInvoker<PlayerMovedEvent> _playerMovedEvent;

    private readonly ConcurrentQueue<Vector2> _cursorDeltaQueue = new();
    private readonly List<Vector2> _cursorDeltaBuffer = [];

    private bool _inputEnabled;
    private bool _windowUnfocused;
    private bool _savedMouseLookState;

    public PlayerControllerSystem(
        in IInputService inputService,
        in IWindowContext windowContext,
        in ControlSettings controlSettings,
        in EventInvoker<PlayerMovedEvent> playerMovedEvent
    ) {
        _inputService = inputService;
        _controlSettings = controlSettings;
        _playerMovedEvent = playerMovedEvent;

        windowContext.Update += OnWindowUpdate;
        windowContext.Focused += OnWindowFocused;
        windowContext.Unfocused += OnWindowUnfocused;
    }

    public void SetInputEnabled(bool enabled)
    {
        if (_inputEnabled == enabled)
        {
            return;
        }

        _inputEnabled = enabled;
        if (enabled)
        {
            _inputService.CursorOptions = CursorOptions.Hidden | CursorOptions.Locked;
            _ = _inputService.CursorDelta;  //  ! HACKY Consume delta state
        }
        else
        {
            _inputService.CursorOptions = CursorOptions.None;
        }
    }

    private void OnWindowUpdate(double delta)
    {
        if (!WaywardBeyond.IsPlaying())
        {
            _cursorDeltaQueue.Clear();
            return;
        }

        _cursorDeltaQueue.Enqueue(_inputService.CursorDelta);
    }

    private void OnWindowFocused()
    {
        SetInputEnabled(_savedMouseLookState);
        _windowUnfocused = false;
    }

    private void OnWindowUnfocused()
    {
        _savedMouseLookState = _inputEnabled;
        SetInputEnabled(false);
        _windowUnfocused = true;
    }

    private struct ForEachAction : IForEachRef<PlayerComponent, PhysicsComponent>
    {
        public PlayerControllerSystem Owner;

        public void Execute(float delta, DataStore store, int entity, ref Ref<PlayerComponent> player, ref Ref<PhysicsComponent> physics)
        {
            while (Owner._cursorDeltaQueue.TryDequeue(out Vector2 cursorDelta))
            {
                Owner._cursorDeltaBuffer.Add(cursorDelta);
            }

            if (!store.TryGet(entity, out TransformComponent transform))
            {
                Owner._cursorDeltaBuffer.Clear();
                return;
            }

            ref PhysicsComponent physicsValue = ref physics.Write;

            var playerMovedEvent = new PlayerMovedEvent(transform.Position);
            Owner._playerMovedEvent.Invoke(playerMovedEvent);

            physicsValue.Torque += -physicsValue.Torque * delta * ANGULAR_DECELERATION;
            if (physicsValue.Torque.LengthSquared() <= 0.00001f)
            {
                physicsValue.Torque = new Vector3();
            }

            physicsValue.Velocity += -physicsValue.Velocity * delta * DECELERATION;
            if (physicsValue.Velocity.LengthSquared() <= 0.00001f)
            {
                physicsValue.Velocity = new Vector3();
            }

            if (!Owner._inputEnabled || Owner._windowUnfocused)
            {
                Owner._cursorDeltaBuffer.Clear();
                return;
            }

            if (!Owner._inputService.IsKeyHeld(Key.Alt))
            {
                float sensitivityModifier = Owner._controlSettings.LookSensitivity / 5f;

                //  Process all cursor deltas that have been recorded between ticks
                for (var i = 0; i < Owner._cursorDeltaBuffer.Count; i++)
                {
                    var cursorDelta = Owner._cursorDeltaBuffer[i];
                    Rotate(ref physicsValue, transform, new Vector3(0, -cursorDelta.X, 0) * MOUSE_SENSITIVITY * sensitivityModifier);
                    Rotate(ref physicsValue, transform, new Vector3(-cursorDelta.Y, 0, 0) * MOUSE_SENSITIVITY * sensitivityModifier);
                }
                Owner._cursorDeltaBuffer.Clear();
            }

            Vector3 forward = transform.GetForward();
            Vector3 right = transform.GetRight();
            Vector3 up = transform.GetUp();

            var velocity = new Vector3();

            if (Owner._inputService.IsKeyHeld(Key.W))
            {
                velocity -= forward;
            }

            if (Owner._inputService.IsKeyHeld(Key.S))
            {
                velocity += forward;
            }

            if (Owner._inputService.IsKeyHeld(Key.D))
            {
                velocity += right;
            }

            if (Owner._inputService.IsKeyHeld(Key.A))
            {
                velocity -= right;
            }

            if (Owner._inputService.IsKeyHeld(Key.Space))
            {
                velocity += up;
            }

            if (Owner._inputService.IsKeyHeld(Key.Control))
            {
                velocity -= up;
            }

            if (Owner._inputService.IsKeyHeld(Key.Q))
            {
                Rotate(ref physicsValue, transform, new Vector3(0, 0, ROLL_RATE * delta));
            }

            if (Owner._inputService.IsKeyHeld(Key.E))
            {
                Rotate(ref physicsValue, transform, new Vector3(0, 0, -ROLL_RATE * delta));
            }

            physicsValue.Velocity += velocity * BASE_SPEED * delta;
        }
    }

    public void Tick(float delta, DataStore store)
    {
        ForEachAction action = new() { Owner = this };
        store.QueryRef<PlayerComponent, PhysicsComponent, ForEachAction>(delta, ref action);
    }

    private static void Rotate(ref PhysicsComponent physics, TransformComponent transform, Vector3 rotation)
    {
        physics.Torque += Vector3.Transform(rotation, transform.Orientation);
    }
}