using System;
using System.Numerics;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.IO;

public interface IInputService
{
    InputDevice[] Devices { get; }
    InputDevice[] Mice { get; }
    InputDevice[] Keyboards { get; }
    InputDevice[] Gamepads { get; }
    InputDevice[] Joysticks { get; }
    InputDevice[] UnknownDevices { get; }

    EventHandler<ClickedEventArgs> Clicked { get; set; }
    EventHandler<ClickedEventArgs> DoubleClicked { get; set; }
    EventHandler<ScrolledEventArgs> Scrolled { get; set; }
    EventHandler<KeyEventArgs> KeyPressed { get; set; }
    EventHandler<KeyEventArgs> KeyReleased { get; set; }
    EventHandler<InputButtonEventArgs> ButtonPressed { get; set; }
    EventHandler<InputButtonEventArgs> ButtonReleased { get; set; }

    CursorState CursorState { get; set; }
    Vector2 CursorDelta { get; }
    Vector2 CursorPosition { get; set; }

    bool IsMouseHeld(MouseButton mouseButton);
    bool IsMousePressed(MouseButton mouseButton);
    bool IsMouseReleased(MouseButton mouseButton);
    float GetMouseScroll();

    bool IsKeyHeld(Key key);
    bool IsKeyPressed(Key key);
    bool IsKeyReleased(Key key);

    bool IsButtonHeld(InputButton button);
    bool IsButtonPressed(InputButton button);
    bool IsButtonReleased(InputButton button);

    float GetAxis(InputAxis axis);
    float GetAxisDeadzone(InputAxis axis);
    void SetAxisDeadzone(InputAxis axis, float deadzone);
}