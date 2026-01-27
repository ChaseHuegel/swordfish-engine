using System;
using System.Collections.Generic;

namespace Reef;

public sealed class UIController
{
    [Flags]
    public enum MouseButtons
    {
        None = 0,
        Left = 1,
        Right = 2,
        Middle = 4,
    }
    
    public enum Input
    {
        Backspace = 8,
        Tab = 9,
        Enter = 13,
        Shift = 16,
        Control = 17,
        Alt = 18,
        Pause = 19,
        Capslock = 20,
        Esc = 27,
        Space = 32,
        PageUp = 33,
        PageDown = 34,
        End = 35,
        Home = 36,
        LeftArrow = 37,
        UpArrow = 38,
        RightArrow = 39,
        DownArrow = 40,
        Select = 41,
        Print = 42,
        Execute = 43,
        PrintScreen = 44,
        Insert = 45,
        Delete = 46,
        Help = 47,
        D0 = 48,
        D1 = 49,
        D2 = 50,
        D3 = 51,
        D4 = 52,
        D5 = 53,
        D6 = 54,
        D7 = 55,
        D8 = 56,
        D9 = 57,
        A = 65,
        B = 66,
        C = 67,
        D = 68,
        E = 69,
        F = 70,
        G = 71,
        H = 72,
        I = 73,
        J = 74,
        K = 75,
        L = 76,
        M = 77,
        N = 78,
        O = 79,
        P = 80,
        Q = 81,
        R = 82,
        S = 83,
        T = 84,
        U = 85,
        V = 86,
        W = 87,
        X = 88,
        Y = 89,
        Z = 90,
        LeftWindows = 91,
        RightWindows = 92,
        Application = 93,
        Sleep = 95,
        Numpad0 = 96,
        Numpad1 = 97,
        Numpad2 = 98,
        Numpad3 = 99,
        Numpad4 = 100,
        Numpad5 = 101,
        Numpad6 = 102,
        Numpad7 = 103,
        Numpad8 = 104,
        Numpad9 = 105,
        Multiply = 106,
        Add = 107,
        Separator = 108,
        Subtract = 109,
        Decimal = 110,
        Divide = 111,
        F1 = 112,
        F2 = 113,
        F3 = 114,
        F4 = 115,
        F5 = 116,
        F6 = 117,
        F7 = 118,
        F8 = 119,
        F9 = 120,
        F10 = 121,
        F11 = 122,
        F12 = 123,
        F13 = 124,
        F14 = 125,
        F15 = 126,
        F16 = 127,
        F17 = 128,
        F18 = 120,
        F19 = 130,
        F20 = 131,
        F21 = 132,
        F22 = 133,
        F23 = 134,
        F24 = 135,
        Numlock = 144,
        ScrollLock = 145,
        Slash,
    }
    
    private readonly struct InputState<T>(T previous, T current)
    {
        public readonly T Previous = previous;
        public readonly T Current = current;
    }

    private readonly struct Position(int x, int y) : IEquatable<Position>
    {
        public readonly int X = x;
        public readonly int Y = y;

        public bool Equals(Position other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object? obj)
        {
            return obj is Position other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }
    }

    private readonly struct InteractionState(InputState<bool> hovering, InputState<bool> button)
    {
        public readonly InputState<bool> Hovering = hovering;
        public readonly InputState<bool> Button = button;
    }

    private InputState<Position> _cursor;
    private InputState<bool> _leftMouse;
    private InputState<bool> _rightMouse;
    private InputState<bool> _middleMouse;
    private string? _lastInteractedID;
    private readonly List<Input> _inputBuffer = [];
    private readonly Dictionary<string, InteractionState> _interactionStates = [];
    
    public void UpdateMouse(int x, int y, MouseButtons downMouseButtons)
    {
        _cursor = new InputState<Position>(previous: _cursor.Current, current: new Position(x, y));
        _leftMouse = new InputState<bool>(previous: _leftMouse.Current, current: (downMouseButtons & MouseButtons.Left) == MouseButtons.Left);
        _rightMouse = new InputState<bool>(previous: _rightMouse.Current, current: (downMouseButtons & MouseButtons.Right) == MouseButtons.Right);
        _middleMouse = new InputState<bool>(previous: _middleMouse.Current, current: (downMouseButtons & MouseButtons.Middle) == MouseButtons.Middle);
        
        //  On click, unfocus any currently focused element.
        //  This is intended to only unfocus if clicking outside any focusable element.
        //  Rather than checking explicitly if something is under the cursor here,
        //  state will be ensured later when element interaction states are updated.
        if (IsLeftPressed())
        {
            _lastInteractedID = null;
        }
    }

    public void UpdateInputBuffer(IEnumerable<Input> buffer)
    {
        lock (_inputBuffer)
        {
            _inputBuffer.Clear();
            foreach (Input input in buffer)
            {
                _inputBuffer.Add(input);
                
                //  On esc, unfocus any currently focused element.
                if (input == Key.Esc)
                {
                    _lastInteractedID = null;
                }
            }
        }
    }
    
    internal bool IsLeftPressed() => IsPressed(_leftMouse);
    internal bool IsLeftReleased() => IsReleased(_leftMouse);
    internal bool IsLeftHeld() => IsHeld(_leftMouse);
    
    internal bool IsRightPressed() => IsPressed(_rightMouse);
    internal bool IsRightReleased() => IsReleased(_rightMouse);
    internal bool IsRightHeld() => IsHeld(_rightMouse);

    internal bool IsClicked(string id) => _interactionStates.TryGetValue(id, out InteractionState interactionState) && IsPressed(interactionState.Button);
    internal bool IsReleased(string id) => _interactionStates.TryGetValue(id, out InteractionState interactionState) && IsReleased(interactionState.Button);
    internal bool IsHeld(string id) => _interactionStates.TryGetValue(id, out InteractionState interactionState) && IsHeld(interactionState.Button);

    internal bool IsHovering(string id) => _interactionStates.TryGetValue(id, out InteractionState interactionState) && interactionState.Hovering.Current;
    internal bool IsEntering(string id) => _interactionStates.TryGetValue(id, out InteractionState interactionState) && IsPressed(interactionState.Hovering);
    internal bool IsExiting(string id) => _interactionStates.TryGetValue(id, out InteractionState interactionState) && IsReleased(interactionState.Hovering);
    
    internal bool IsFocused(string id) => _lastInteractedID == id;
    
    internal bool IsPressed(Input input)
    {
        lock (_inputBuffer)
        {
            return _inputBuffer.Contains(input);
        }
    }
    
    internal IReadOnlyCollection<Input> GetInputBuffer()
    {
        lock (_inputBuffer)
        {
            return _inputBuffer;
        }
    }
    
    internal void UpdateInteraction(string id, IntRect rect)
    {
        Position cursorPos = _cursor.Current;
        bool hovered = rect.Contains(cursorPos.X, cursorPos.Y);
        bool interacting = hovered && IsPressed(_leftMouse);

        if (interacting)
        {
            _lastInteractedID = id;
        }

        if (_interactionStates.TryGetValue(id, out InteractionState interactionState))
        {
            var hoveredState = new InputState<bool>(previous: interactionState.Hovering.Current, current: hovered);
            var clickedState = new InputState<bool>(previous: interactionState.Button.Current, current: interacting);
            _interactionStates[id] = new InteractionState(hoveredState, clickedState);
        }
        else
        {
            var hoveredState = new InputState<bool>(previous: false, current: hovered);
            var clickedState = new InputState<bool>(previous: false, current: interacting);
            _interactionStates.Add(id, new InteractionState(hoveredState, clickedState));
        }
    }

    private bool IsPressed(InputState<bool> state)
    {
        return !state.Previous && state.Current;
    }
    
    private bool IsReleased(InputState<bool> state)
    {
        return state.Previous && !state.Current;
    }
    
    private bool IsHeld(InputState<bool> state)
    {
        return state.Previous && state.Current;
    }

    private bool HasMoved(InputState<Position> state)
    {
        return !state.Previous.Equals(state.Current);
    }
}