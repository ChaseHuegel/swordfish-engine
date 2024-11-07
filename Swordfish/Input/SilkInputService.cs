using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Silk.NET.Input;
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

    private readonly Stopwatch _cursorMovementStopwatch = new();
    private readonly object _cursorDataLock = new();

    private Vector2 _cursorDelta;
    public Vector2 CursorDelta {
        get
        {
            lock (_cursorDataLock)
            {
                return _cursorMovementStopwatch.ElapsedMilliseconds < 2 ? _cursorDelta : Vector2.Zero;
            }
        }
    }

    private Vector2 _cursorPosition;
    public Vector2 CursorPosition {
        get {
            lock (_cursorDataLock)
            {
                return _cursorPosition;
            }
        }
        set {
            lock (_cursorDataLock)
            {
                _cursorPosition = value;
            }
        }
    }

    public CursorState CursorState {
        get => GetCursorState();
        set => SetCursorState(value);
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

    public SilkInputService(IInputContext context)
    {
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
        foreach (IMouse? mouse in context.Mice)
        {
            mouse.Click += OnClick;
            mouse.DoubleClick += OnDoubleClick;
            mouse.Scroll += OnScroll;
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.MouseMove += OnMouseMove;
        }

        foreach (MouseButton mouseButton in Enum.GetValues<MouseButton>())
        {
            _mouseInputMap.TryAdd(mouseButton, new InputRecord());
        }

        foreach (IKeyboard? keyboard in context.Keyboards)
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

    private void SetCursorState(CursorState value)
    {
        switch (value)
        {
            case CursorState.NORMAL:
                _mainMouse.Cursor.IsConfined = false;
                _mainMouse.Cursor.CursorMode = CursorMode.Normal;
                break;

            case CursorState.HIDDEN:
                _mainMouse.Cursor.IsConfined = false;
                _mainMouse.Cursor.CursorMode = CursorMode.Hidden;
                break;

            case CursorState.LOCKED:
                _mainMouse.Cursor.IsConfined = false;
                _mainMouse.Cursor.CursorMode = CursorMode.Disabled;
                break;

            case CursorState.CAPTURED:
                _mainMouse.Cursor.IsConfined = true;
                _mainMouse.Cursor.CursorMode = CursorMode.Normal;
                break;
        }
    }

    private CursorState GetCursorState()
    {
        return _mainMouse.Cursor.IsConfined ? CursorState.CAPTURED : _mainMouse.Cursor.CursorMode.ToCursorState();
    }

    private void OnClick(IMouse mouse, Silk.NET.Input.MouseButton button, Vector2 position)
    {
        Clicked?.Invoke(
            new InputDevice(mouse.Index, mouse.Name),
            new ClickedEventArgs(button.ToSwordfishMouseButton(), position)
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
        InputRecord inputRecord = _mouseInputMap[swordfishButton];
        inputRecord.Count += 1;
        inputRecord.LastInput.Restart();
    }

    private void OnMouseUp(IMouse mouse, Silk.NET.Input.MouseButton button)
    {
        MouseButton swordfishButton = button.ToSwordfishMouseButton();
        InputRecord inputRecord = _mouseInputMap[swordfishButton];
        inputRecord.Count -= 1;
        inputRecord.LastRelease.Restart();
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        lock (_cursorDataLock)
        {
            _cursorMovementStopwatch.Restart();
            _cursorDelta = position - _cursorPosition;
            _cursorPosition = position;
        }
    }
}
