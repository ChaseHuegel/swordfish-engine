using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class PlayerControllerSystem
    : EntitySystem<PlayerComponent, PhysicsComponent>
{
    private const float MOUSE_SENSITIVITY = 0.15f;
    private const float BASE_SPEED = 10;
    private const float ROLL_RATE = 60;
    private const float DECELERATION = 0.5f;

    private readonly IInputService _inputService;
    private readonly ControlSettings _controlSettings;
    
    private bool _mouseLookEnabled;
    private bool _windowUnfocused;
    private bool _savedMouseLookState;

    public PlayerControllerSystem(
        in IInputService inputService,
        in IShortcutService shortcutService,
        in IWindowContext windowContext,
        in ControlSettings controlSettings
    ) {
        _inputService = inputService;
        _controlSettings = controlSettings;
        
        Shortcut mouseLookShortcut = new(
            "Toggle Mouselook",
            "Interaction",
            ShortcutModifiers.None,
            Key.Tab,
            Shortcut.DefaultEnabled,
            ToggleMouselook);
        shortcutService.RegisterShortcut(mouseLookShortcut);
        
        windowContext.Focused += OnWindowFocused;
        windowContext.Unfocused += OnWindowUnfocused;
    }

    public bool IsMouseLookEnabled()
    {
        return _mouseLookEnabled;
    }
    
    public void SetMouseLook(bool enabled)
    {
        _mouseLookEnabled = enabled;
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

    private void ToggleMouselook()
    {
        SetMouseLook(!_mouseLookEnabled);
    }

    protected override void OnTick(float delta, DataStore store, int entity, ref PlayerComponent player, ref PhysicsComponent physics)
    {
        if (_windowUnfocused)
        {
            return;
        }
        
        if (!store.TryGet(entity, out TransformComponent transform))
        {
            return;
        }
        
        if (_mouseLookEnabled && !_inputService.IsKeyHeld(Key.Alt))
        {
            Vector2 cursorDelta = _inputService.CursorDelta;
            float sensitivityModifier = _controlSettings.LookSensitivity / 5f;
            // Rotate(ref physics, transform, new Vector3(0, -cursorDelta.X, 0) * MOUSE_SENSITIVITY * sensitivityModifier, true);
            // Rotate(ref physics, transform, new Vector3(-cursorDelta.Y, 0, 0) * MOUSE_SENSITIVITY * sensitivityModifier, true);
        }
        
        Vector3 forward = transform.GetForward();
        Vector3 right = transform.GetRight();
        Vector3 up = transform.GetUp();
        
        var velocity = new Vector3();
        
        if (_inputService.IsKeyHeld(Key.W))
        {
            velocity -= forward;
        }
        
        if (_inputService.IsKeyHeld(Key.S))
        {
            velocity += forward;
        }
        
        if (_inputService.IsKeyHeld(Key.D))
        {
            velocity += right;
        }
        
        if (_inputService.IsKeyHeld(Key.A))
        {
            velocity -= right;
        }
        
        if (_inputService.IsKeyHeld(Key.Space))
        {
            velocity += up;
        }
        
        if (_inputService.IsKeyHeld(Key.Control))
        {
            velocity -= up;
        }
        
        if (_inputService.IsKeyHeld(Key.Q))
        {
            // Rotate(ref transform, new Vector3(0, 0, ROLL_RATE * delta), true);
        }
        
        if (_inputService.IsKeyHeld(Key.E))
        {
            // Rotate(ref transform, new Vector3(0, 0, -ROLL_RATE * delta), true);
        }
        
        physics.Velocity += -physics.Velocity * delta * DECELERATION;
        if (physics.Velocity.LengthSquared() <= 0.001f)
        {
            physics.Velocity = new Vector3();
        }

        physics.Torque += -physics.Torque * delta * DECELERATION;
        if (physics.Torque.LengthSquared() <= 0.001f)
        {
            physics.Torque = new Vector3();
        }
        
        physics.Velocity += velocity * BASE_SPEED * delta;
    }

    private void Rotate(ref PhysicsComponent physics, TransformComponent transform, Vector3 rotation, bool local = false)
    {
        var eulerQuaternion = Quaternion.CreateFromYawPitchRoll(rotation.Y * MathS.DEGREES_TO_RADIANS, rotation.X * MathS.DEGREES_TO_RADIANS, rotation.Z * MathS.DEGREES_TO_RADIANS);
        if (local)
        {
            transform.Orientation = Quaternion.Multiply(transform.Orientation, eulerQuaternion);
        }
        else
        {
            transform.Orientation = Quaternion.Multiply(eulerQuaternion, transform.Orientation);
        }
    }
    
    private void OnWindowFocused()
    {
        SetMouseLook(_savedMouseLookState);
        _windowUnfocused = false;
    }
    
    private void OnWindowUnfocused()
    {
        _savedMouseLookState = _mouseLookEnabled;
        SetMouseLook(false);
        _windowUnfocused = true;
    }
}