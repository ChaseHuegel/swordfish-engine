using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Debugger = Swordfish.Library.Diagnostics.Debugger;

namespace Swordfish.Input;

public class SilkInputService : IInputService
{
    private const int InputBufferMs = 10;

    public InputDevice[] Devices { get; private set; } = Array.Empty<InputDevice>();

    public InputDevice[] Mice { get; private set; } = Array.Empty<InputDevice>();

    public InputDevice[] Keyboards { get; private set; } = Array.Empty<InputDevice>();

    public InputDevice[] Gamepads { get; private set; } = Array.Empty<InputDevice>();

    public InputDevice[] Joysticks { get; private set; } = Array.Empty<InputDevice>();

    public InputDevice[] UnknownDevices { get; private set; } = Array.Empty<InputDevice>();

    public EventHandler<ClickedEventArgs>? Clicked { get; set; }
    public EventHandler<ScrolledEventArgs>? Scrolled { get; set; }
    public EventHandler<KeyEventArgs>? KeyPressed { get; set; }
    public EventHandler<KeyEventArgs>? KeyReleased { get; set; }
    public EventHandler<InputButtonEventArgs>? ButtonPressed { get; set; }
    public EventHandler<InputButtonEventArgs>? ButtonReleased { get; set; }

    public CursorState CursorState { get; set; }
    public Vector3 CursorDelta { get; private set; }
    public Vector3 CursorPosition { get; set; }

    private Silk.NET.Input.IInputContext? Context;
    private readonly ConcurrentDictionary<Key, KeyInputRecord> KeyInputMap = new();

    private class KeyInputRecord
    {
        public int Count;
        public readonly Stopwatch LastInput = Stopwatch.StartNew();
        public readonly Stopwatch LastRelease = Stopwatch.StartNew();
    }

    public SilkInputService(Silk.NET.Input.IInputContext context)
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

        foreach (var keyboard in Context.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyUp += OnKeyUp;
        }

        foreach (Key key in Enum.GetValues<Key>())
            KeyInputMap.TryAdd(key, new KeyInputRecord());

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
        throw new NotImplementedException();
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
        KeyInputRecord inputRecord = KeyInputMap[key];
        return inputRecord.Count > 0 && inputRecord.LastInput.ElapsedMilliseconds <= InputBufferMs;
    }

    public bool IsKeyReleased(Key key)
    {
        KeyInputRecord inputRecord = KeyInputMap[key];
        return inputRecord.Count == 0 && inputRecord.LastRelease.ElapsedMilliseconds <= InputBufferMs;
    }

    public bool IsMouseHeld(MouseButton mouseButton)
    {
        throw new NotImplementedException();
    }

    public bool IsMousePressed(MouseButton mouseButton)
    {
        throw new NotImplementedException();
    }

    public bool IsMouseReleased(MouseButton mouseButton)
    {
        throw new NotImplementedException();
    }

    public void SetAxisDeadzone(InputAxis axis, float deadzone)
    {
        throw new NotImplementedException();
    }

    private void OnKeyDown(Silk.NET.Input.IKeyboard keyboard, Silk.NET.Input.Key key, int index)
    {
        Key swordfishKey = key.ToSwordfishKey();
        KeyInputRecord inputRecord = KeyInputMap[swordfishKey];
        inputRecord.Count += 1;
        inputRecord.LastInput.Restart();

        KeyPressed?.Invoke(new InputDevice(keyboard.Index, keyboard.Name), new KeyEventArgs(swordfishKey));
    }

    private void OnKeyUp(Silk.NET.Input.IKeyboard keyboard, Silk.NET.Input.Key key, int index)
    {
        Key swordfishKey = key.ToSwordfishKey();
        KeyInputRecord inputRecord = KeyInputMap[swordfishKey];
        inputRecord.Count -= 1;
        inputRecord.LastRelease.Restart();

        KeyReleased?.Invoke(new InputDevice(keyboard.Index, keyboard.Name), new KeyEventArgs(swordfishKey));
    }
}
