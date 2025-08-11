using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class PlayerControllerSystem
    : EntitySystem<PlayerComponent, TransformComponent>
{
    private const float MOUSE_SENSITIVITY = 0.15f;
    private const float BASE_SPEED = 20;
    private const float ROLL_RATE = 60;

    private readonly IInputService _inputService;
    
    private bool _mouseLookEnabled;

    public PlayerControllerSystem(in IInputService inputService, in IShortcutService shortcutService)
    {
        _inputService = inputService;
        
        Shortcut mouseLookShortcut = new(
            "Toggle Mouselook",
            "Interaction",
            ShortcutModifiers.None,
            Key.Tab,
            Shortcut.DefaultEnabled,
            ToggleMouselook);
        shortcutService.RegisterShortcut(mouseLookShortcut);
    }

    private void ToggleMouselook()
    {
        _mouseLookEnabled = !_mouseLookEnabled;
        if (_mouseLookEnabled)
        {
            _inputService.CursorOptions = CursorOptions.Hidden | CursorOptions.Locked;
            _ = _inputService.CursorDelta;  //  ! HACKY Consume delta state
        }
        else
        {
            _inputService.CursorOptions = CursorOptions.None;
        }
    }

    protected override void OnTick(float delta, DataStore store, int entity, ref PlayerComponent player, ref TransformComponent transform)
    {
        if (_mouseLookEnabled && !_inputService.IsKeyHeld(Key.Alt))
        {
            Vector2 cursorDelta = _inputService.CursorDelta;
            Rotate(ref transform, new Vector3(0, -cursorDelta.X, 0) * MOUSE_SENSITIVITY, true);
            Rotate(ref transform, new Vector3(-cursorDelta.Y, 0, 0) * MOUSE_SENSITIVITY, true);
        }
        
        Vector3 forward = transform.GetForward();
        Vector3 right = transform.GetRight();
        Vector3 up = transform.GetUp();

        if (_inputService.IsKeyHeld(Key.W))
        {
            transform.Position -= forward * BASE_SPEED * delta;
        }

        if (_inputService.IsKeyHeld(Key.S))
        {
            transform.Position += forward * BASE_SPEED * delta;
        }

        if (_inputService.IsKeyHeld(Key.D))
        {
            transform.Position += right * BASE_SPEED * delta;
        }

        if (_inputService.IsKeyHeld(Key.A))
        {
            transform.Position -= right * BASE_SPEED * delta;
        }

        if (_inputService.IsKeyHeld(Key.Space))
        {
            transform.Position += up * BASE_SPEED * delta;
        }

        if (_inputService.IsKeyHeld(Key.Control))
        {
            transform.Position -= up * BASE_SPEED * delta;
        }
        
        if (_inputService.IsKeyHeld(Key.Q))
        {
            Rotate(ref transform, new Vector3(0, 0, ROLL_RATE * delta), true);
        }
        
        if (_inputService.IsKeyHeld(Key.E))
        {
            Rotate(ref transform, new Vector3(0, 0, -ROLL_RATE * delta), true);
        }
    }
    
    private void Rotate(ref TransformComponent transform, Vector3 rotation, bool local = false)
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
}