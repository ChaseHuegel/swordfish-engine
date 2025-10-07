using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using Silk.NET.Input.Extensions;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Swordfish.Library.IO;
using Key = Swordfish.Library.IO.Key;
using MouseButton = Swordfish.Library.IO.MouseButton;

namespace Swordfish.Input;

public class SilkInputService : IInputService
{
    private const int INPUT_BUFFER_MS = 1;

    public InputDevice[] Devices { get; }

    public InputDevice[] Mice { get; }

    public InputDevice[] Keyboards { get; }

    public InputDevice[] Gamepads { get; }

    public InputDevice[] Joysticks { get; }

    public InputDevice[] UnknownDevices { get; }

    public EventHandler<ClickedEventArgs>? Clicked { get; set; }
    public EventHandler<ClickedEventArgs>? DoubleClicked { get; set; }
    public EventHandler<ScrolledEventArgs>? Scrolled { get; set; }
    public EventHandler<KeyEventArgs>? KeyPressed { get; set; }
    public EventHandler<KeyEventArgs>? KeyReleased { get; set; }
    public EventHandler<InputButtonEventArgs>? ButtonPressed { get; set; }
    public EventHandler<InputButtonEventArgs>? ButtonReleased { get; set; }

    private readonly IWindow _window;
    
    private readonly Stopwatch _cursorMovementStopwatch = new();
    private readonly object _cursorLock = new();

    private Vector2 _cursorDelta;
    public Vector2 CursorDelta {
        get
        {
            lock (_cursorLock)
            {
                if (_cursorMovementStopwatch.ElapsedMilliseconds >= 2)
                {
                    _cursorDelta = Vector2.Zero;
                    return Vector2.Zero;
                }
                
                //  Clear the delta after it has been read.
                //  The delta is accumulated over time in Update.
                //  
                //  This is necessary to not miss changes between
                //  queries of the delta when accessing from a thread
                //  that is not synchronized with the window's update.
                //  
                //  This isn't ideal because multiple readers
                //  will not see the same value. Need a better solution eventually.
                Vector2 delta = _cursorDelta;
                _cursorDelta = Vector2.Zero;
                return delta;
            }
        }
    }

    private Vector2 _cursorPosition;
    public Vector2 CursorPosition {
        get {
            lock (_cursorLock)
            {
                return _cursorPosition;
            }
        }
        set {
            lock (_cursorLock)
            {
                _cursorPosition = value;
                _mainMouse.Position = value;
            }
        }
    }

    private CursorOptions _cursorOptions;
    public CursorOptions CursorOptions
    {
        get
        {
            lock (_cursorLock)
            {
                return _cursorOptions;
            }
        }
        set
        {
            //  Checking outside the lock intentionally.
            //  Changes are atomic and don't want to waste taking the lock if nothing will change.
            //  It does feel like there is a bug here I'm overlooking. May need to move into the lock later.
            if (_cursorOptions == value)
            {
                return;
            }
            
            lock (_cursorLock)
            {
                _cursorOptions = value;
                _mainMouse.Cursor.CursorMode = (value & CursorOptions.Hidden) == CursorOptions.Hidden ? CursorMode.Hidden : CursorMode.Normal;
            }
        }
    }

    private volatile float _lastScroll;

    private readonly IMouse _mainMouse;
    private readonly ConcurrentDictionary<Key, InputRecord> _keyInputMap = new();
    private readonly ConcurrentDictionary<MouseButton, InputRecord> _mouseInputMap = new();
    private readonly InputRecord _scrollRecord = new();

    private class InputRecord
    {
        public int Count;
        public readonly Stopwatch LastInput = Stopwatch.StartNew();
        public readonly Stopwatch LastRelease = Stopwatch.StartNew();
    }

    public SilkInputService(IInputContext context, IWindow window)
    {
        _window = window;
        _window.Update += OnWindowUpdate;
        
        Mice = context.Mice.Select(x => new InputDevice(x.Index, x.Name)).ToArray();
        Keyboards = context.Keyboards.Select(x => new InputDevice(x.Index, x.Name)).ToArray();
        Gamepads = context.Gamepads.Select(x => new InputDevice(x.Index, x.Name)).ToArray();
        Joysticks = context.Joysticks.Select(x => new InputDevice(x.Index, x.Name)).ToArray();
        UnknownDevices = context.OtherDevices.Select(x => new InputDevice(x.Index, x.Name)).ToArray();
        Devices = new List<InputDevice>()
            .Concat(Mice)
            .Concat(Keyboards)
            .Concat(Gamepads)
            .Concat(Joysticks)
            .Concat(UnknownDevices)
            .ToArray();

        _mainMouse = context.Mice[0];
        foreach (IMouse mouse in context.Mice)
        {
            mouse.DoubleClick += OnDoubleClick;
            mouse.Scroll += OnScroll;
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
        }

        foreach (MouseButton mouseButton in Enum.GetValues<MouseButton>())
        {
            _mouseInputMap.TryAdd(mouseButton, new InputRecord());
        }

        foreach (IKeyboard keyboard in context.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyUp += OnKeyUp;
        }

        foreach (Key key in Enum.GetValues<Key>())
        {
            _keyInputMap.TryAdd(key, new InputRecord());
        }
    }

    public float GetAxis(InputAxis axis)
    {
        throw new NotImplementedException();
    }

    public float GetAxisDeadzone(InputAxis axis)
    {
        throw new NotImplementedException();
    }

    public float GetMouseScroll()
    {
        return _scrollRecord.LastInput.ElapsedMilliseconds < INPUT_BUFFER_MS ? _lastScroll : 0f;
    }

    public bool IsButtonHeld(InputButton button)
    {
        throw new NotImplementedException();
    }

    public bool IsButtonPressed(InputButton button)
    {
        throw new NotImplementedException();
    }

    public bool IsButtonReleased(InputButton button)
    {
        throw new NotImplementedException();
    }

    public bool IsKeyHeld(Key key)
    {
        return _keyInputMap[key].Count > 0;
    }

    public bool IsKeyPressed(Key key)
    {
        InputRecord inputRecord = _keyInputMap[key];
        return inputRecord.LastInput.ElapsedMilliseconds < INPUT_BUFFER_MS;
    }

    public bool IsKeyReleased(Key key)
    {
        InputRecord inputRecord = _keyInputMap[key];
        return inputRecord.LastRelease.ElapsedMilliseconds < INPUT_BUFFER_MS;
    }

    public bool IsMouseHeld(MouseButton mouseButton)
    {
        return _mouseInputMap[mouseButton].Count > 0;
    }

    public bool IsMousePressed(MouseButton mouseButton)
    {
        InputRecord inputRecord = _mouseInputMap[mouseButton];
        return inputRecord.LastInput.ElapsedMilliseconds <= INPUT_BUFFER_MS;
    }

    public bool IsMouseReleased(MouseButton mouseButton)
    {
        InputRecord inputRecord = _mouseInputMap[mouseButton];
        return inputRecord.LastRelease.ElapsedMilliseconds <= INPUT_BUFFER_MS;
    }

    public void SetAxisDeadzone(InputAxis axis, float deadzone)
    {
        throw new NotImplementedException();
    }

    private void OnKeyDown(IKeyboard keyboard, Silk.NET.Input.Key key, int index)
    {
        Key swordfishKey = key.ToSwordfishKey();
        InputRecord inputRecord = _keyInputMap[swordfishKey];
        inputRecord.Count += 1;
        inputRecord.LastInput.Restart();

        KeyPressed?.Invoke(
            new InputDevice(keyboard.Index, keyboard.Name),
            new KeyEventArgs(swordfishKey)
        );
    }

    private void OnKeyUp(IKeyboard keyboard, Silk.NET.Input.Key key, int index)
    {
        Key swordfishKey = key.ToSwordfishKey();
        InputRecord inputRecord = _keyInputMap[swordfishKey];
        inputRecord.Count -= 1;
        inputRecord.LastRelease.Restart();

        KeyReleased?.Invoke(
            new InputDevice(keyboard.Index, keyboard.Name),
            new KeyEventArgs(swordfishKey)
        );
    }

    private void OnDoubleClick(IMouse mouse, Silk.NET.Input.MouseButton button, Vector2 position)
    {
        DoubleClicked?.Invoke(
            new InputDevice(mouse.Index, mouse.Name),
            new ClickedEventArgs(button.ToSwordfishMouseButton(), position)
        );
    }

    private void OnScroll(IMouse mouse, ScrollWheel wheel)
    {
        _lastScroll = wheel.Y;
        _scrollRecord.LastInput.Restart();

        Scrolled?.Invoke(
            new InputDevice(mouse.Index, mouse.Name),
            new ScrolledEventArgs(wheel.Y)
        );
    }

    private void OnMouseDown(IMouse mouse, Silk.NET.Input.MouseButton button)
    {
        MouseButton swordfishButton = button.ToSwordfishMouseButton();
        
        Clicked?.Invoke(
            new InputDevice(mouse.Index, mouse.Name),
            new ClickedEventArgs(swordfishButton, mouse.Position)
        );
        
        InputRecord inputRecord = _mouseInputMap[swordfishButton];
        inputRecord.Count += 1;
        inputRecord.LastInput.Restart();
    }

    private void OnMouseUp(IMouse mouse, Silk.NET.Input.MouseButton button)
    {
        MouseButton swordfishButton = button.ToSwordfishMouseButton();
        InputRecord inputRecord = _mouseInputMap[swordfishButton];
        inputRecord.Count = Math.Max(0, inputRecord.Count - 1);
        inputRecord.LastRelease.Restart();
    }

    private void OnWindowUpdate(double delta)
    {
        lock (_cursorLock)
        {
            Vector2 newMousePosition = _mainMouse.Position;
            _cursorDelta += newMousePosition - _cursorPosition;
            _cursorPosition = newMousePosition;
        
            if (_cursorDelta != Vector2.Zero)
            {
                _cursorMovementStopwatch.Restart();
            }
        
            //  GLFW, or Silk.NET's usage of it, doesn't like cursor state being changed,
            //  and CursorState.Locked doesn't work unless alt+tabbing out and back in.
            //
            //  When changing Normal -> Disabled/Raw -> Normal, the cursor becomes locked to
            //  the center and visible, where it's in a state between Normal and Disabled
            //
            //  Because of these kinds of issues, the cursor modes are manually implemented :(
            //  
            //  Hopefully this is fixed in the future.
            //  Last checked with: Silk.NET 2.22.0
            
            Vector2D<int> windowSize;
            if ((_cursorOptions & CursorOptions.Locked) == CursorOptions.Locked)
            {
                windowSize = _window.Size;
                var center = new Vector2(windowSize.X / 2, windowSize.Y / 2);
                if (newMousePosition == center)
                {
                    return;
                }
            
                _mainMouse.Position = center;
                _cursorPosition = center;
                return;
            }

            //  Mouse.Cursor.IsConfined is only supported by SDL.
            //  Since GLFW is being used, confinement is implemented here.
            if ((_cursorOptions & CursorOptions.Confined) != CursorOptions.Confined)
            {
                return;
            }

            var updateCursor = false;
            windowSize = _window.Size;
            
            if (newMousePosition.X < 0)
            {
                newMousePosition.X = 0;
                updateCursor = true;
            }

            if (newMousePosition.Y < 0)
            {
                newMousePosition.Y = 0;
                updateCursor = true;
            }

            if (newMousePosition.X > windowSize.X)
            {
                newMousePosition.X = windowSize.X;
                updateCursor = true;
            }

            if (newMousePosition.Y > windowSize.Y)
            {
                newMousePosition.Y = windowSize.Y;
                updateCursor = true;
            }

            if (!updateCursor)
            {
                return;
            }

            _mainMouse.Position = newMousePosition;
        }
    }
}
