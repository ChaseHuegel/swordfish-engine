using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Silk.NET.Input;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Util;
using Debugger = Swordfish.Library.Diagnostics.Debugger;
using Key = Swordfish.Library.IO.Key;
using MouseButton = Swordfish.Library.IO.MouseButton;

namespace Swordfish.Input;

public class SilkInputService : IInputService
{
    private const int InputBufferMs = 1;

    public InputDevice[] Devices { get; private set; } = Array.Empty<InputDevice>();

    public InputDevice[] Mice { get; private set; } = Array.Empty<InputDevice>();

    public InputDevice[] Keyboards { get; private set; } = Array.Empty<InputDevice>();

    public InputDevice[] Gamepads { get; private set; } = Array.Empty<InputDevice>();

    public InputDevice[] Joysticks { get; private set; } = Array.Empty<InputDevice>();

    public InputDevice[] UnknownDevices { get; private set; } = Array.Empty<InputDevice>();

    public EventHandler<ClickedEventArgs>? Clicked { get; set; }
    public EventHandler<ClickedEventArgs>? DoubleClicked { get; set; }
    public EventHandler<ScrolledEventArgs>? Scrolled { get; set; }
    public EventHandler<KeyEventArgs>? KeyPressed { get; set; }
    public EventHandler<KeyEventArgs>? KeyReleased { get; set; }
    public EventHandler<InputButtonEventArgs>? ButtonPressed { get; set; }
    public EventHandler<InputButtonEventArgs>? ButtonReleased { get; set; }

    private readonly Stopwatch CursorMovementStopwatch = new();
    private readonly object CursorDataLock = new();

    private Vector2 cursorDelta;
    public Vector2 CursorDelta {
        get
        {
            lock (CursorDataLock)
            {
                return CursorMovementStopwatch.ElapsedMilliseconds < 2 ? cursorDelta : Vector2.Zero;
            }
        }
    }

    private Vector2 cursorPosition;
    public Vector2 CursorPosition {
        get {
            lock (CursorDataLock)
            {
                return cursorPosition;
            }
        }
        set {
            lock (CursorDataLock)
            {
                cursorPosition = value;
            }
        }
    }

    public CursorState CursorState {
        get => GetCursorState();
        set => SetCursorState(value);
    }

    private volatile float LastScroll;

    private readonly IMouse MainMouse;
    private readonly IInputContext Context;
    private readonly ConcurrentDictionary<Key, InputRecord> KeyInputMap = new();
    private readonly ConcurrentDictionary<MouseButton, InputRecord> MouseInputMap = new();
    private readonly InputRecord ScrollRecord = new();

    private class InputRecord
    {
        public int Count;
        public readonly Stopwatch LastInput = Stopwatch.StartNew();
        public readonly Stopwatch LastRelease = Stopwatch.StartNew();
    }

    public SilkInputService(IInputContext context)
    {
        Context = context;

        Mice = Context.Mice.Select(x => new InputDevice(x.Index, x.Name)).ToArray();
        Keyboards = Context.Keyboards.Select(x => new InputDevice(x.Index, x.Name)).ToArray();
        Gamepads = Context.Gamepads.Select(x => new InputDevice(x.Index, x.Name)).ToArray();
        Joysticks = Context.Joysticks.Select(x => new InputDevice(x.Index, x.Name)).ToArray();
        UnknownDevices = Context.OtherDevices.Select(x => new InputDevice(x.Index, x.Name)).ToArray();
        Devices = new List<InputDevice>()
            .Concat(Mice)
            .Concat(Keyboards)
            .Concat(Gamepads)
            .Concat(Joysticks)
            .Concat(UnknownDevices)
            .ToArray();

        MainMouse = Context.Mice[0];
        foreach (var mouse in Context.Mice)
        {
            mouse.Click += OnClick;
            mouse.DoubleClick += OnDoubleClick;
            mouse.Scroll += OnScroll;
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.MouseMove += OnMouseMove;
        }

        foreach (MouseButton mouseButton in Enum.GetValues<MouseButton>())
            MouseInputMap.TryAdd(mouseButton, new InputRecord());

        foreach (var keyboard in Context.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyUp += OnKeyUp;
        }

        foreach (Key key in Enum.GetValues<Key>())
            KeyInputMap.TryAdd(key, new InputRecord());

        Debugger.Log("Input initialized.");
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
        return ScrollRecord.LastInput.ElapsedMilliseconds < InputBufferMs ? LastScroll : 0f;
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
        return KeyInputMap[key].Count > 0;
    }

    public bool IsKeyPressed(Key key)
    {
        InputRecord inputRecord = KeyInputMap[key];
        return inputRecord.LastInput.ElapsedMilliseconds < InputBufferMs;
    }

    public bool IsKeyReleased(Key key)
    {
        InputRecord inputRecord = KeyInputMap[key];
        return inputRecord.LastRelease.ElapsedMilliseconds < InputBufferMs;
    }

    public bool IsMouseHeld(MouseButton mouseButton)
    {
        return MouseInputMap[mouseButton].Count > 0;
    }

    public bool IsMousePressed(MouseButton mouseButton)
    {
        InputRecord inputRecord = MouseInputMap[mouseButton];
        return inputRecord.LastInput.ElapsedMilliseconds <= InputBufferMs;
    }

    public bool IsMouseReleased(MouseButton mouseButton)
    {
        InputRecord inputRecord = MouseInputMap[mouseButton];
        return inputRecord.LastRelease.ElapsedMilliseconds <= InputBufferMs;
    }

    public void SetAxisDeadzone(InputAxis axis, float deadzone)
    {
        throw new NotImplementedException();
    }

    private void OnKeyDown(IKeyboard keyboard, Silk.NET.Input.Key key, int index)
    {
        Key swordfishKey = key.ToSwordfishKey();
        InputRecord inputRecord = KeyInputMap[swordfishKey];
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
        InputRecord inputRecord = KeyInputMap[swordfishKey];
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
                MainMouse.Cursor.IsConfined = false;
                MainMouse.Cursor.CursorMode = CursorMode.Normal;
                break;

            case CursorState.HIDDEN:
                MainMouse.Cursor.IsConfined = false;
                MainMouse.Cursor.CursorMode = CursorMode.Hidden;
                break;

            case CursorState.LOCKED:
                MainMouse.Cursor.IsConfined = false;
                MainMouse.Cursor.CursorMode = CursorMode.Disabled;
                break;

            case CursorState.CAPTURED:
                MainMouse.Cursor.IsConfined = true;
                MainMouse.Cursor.CursorMode = CursorMode.Normal;
                break;
        }
    }

    private CursorState GetCursorState()
    {
        if (MainMouse.Cursor.IsConfined)
            return CursorState.CAPTURED;

        return MainMouse.Cursor.CursorMode.ToCursorState();
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
        LastScroll = wheel.Y;
        ScrollRecord.LastInput.Restart();

        Scrolled?.Invoke(
            new InputDevice(mouse.Index, mouse.Name),
            new ScrolledEventArgs(wheel.Y)
        );
    }

    private void OnMouseDown(IMouse mouse, Silk.NET.Input.MouseButton button)
    {
        MouseButton swordfishButton = button.ToSwordfishMouseButton();
        InputRecord inputRecord = MouseInputMap[swordfishButton];
        inputRecord.Count += 1;
        inputRecord.LastInput.Restart();
    }

    private void OnMouseUp(IMouse mouse, Silk.NET.Input.MouseButton button)
    {
        MouseButton swordfishButton = button.ToSwordfishMouseButton();
        InputRecord inputRecord = MouseInputMap[swordfishButton];
        inputRecord.Count -= 1;
        inputRecord.LastRelease.Restart();
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        lock (CursorDataLock)
        {
            CursorMovementStopwatch.Restart();
            cursorDelta = position - cursorPosition;
            cursorPosition = position;
        }
    }
}
