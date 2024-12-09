using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class PlayerControllerSystem(in IInputService inputService)
    : EntitySystem<PlayerComponent, TransformComponent>
{
    private const float MOUSE_SENSITIVITY = 0.05f;
    private const float BASE_SPEED = 10;

    private readonly IInputService _inputService = inputService;
    
    private Vector2 _lastMousePosition;

    protected override void OnTick(float delta, DataStore store, int entity, ref PlayerComponent player, ref TransformComponent transform)
    {
        Vector2 cursorPosition = _inputService.CursorPosition;
        if (!_inputService.IsKeyHeld(Key.Alt))
        {
            _inputService.CursorState = CursorState.Locked;
            Vector2 cursorDelta = cursorPosition - _lastMousePosition;
            Rotate(ref transform, new Vector3(0, -cursorDelta.X, 0) * MOUSE_SENSITIVITY, true);
            Rotate(ref transform, new Vector3(-cursorDelta.Y, 0, 0) * MOUSE_SENSITIVITY, true);
        }
        else
        {
            _inputService.CursorState = CursorState.Normal;
        }
        _lastMousePosition = cursorPosition;
        
        Vector3 forward = transform.GetForward();
        Vector3 right = transform.GetRight();

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
            transform.Position += new Vector3(0, BASE_SPEED * delta, 0);
        }

        if (_inputService.IsKeyHeld(Key.Control))
        {
            transform.Position -= new Vector3(0, BASE_SPEED * delta, 0);
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