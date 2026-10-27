using System;
using System.Collections.Generic;
using System.Numerics;
using Reef.Text;
using Reef.UI;

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
    
    public readonly record struct Input
    {
        public readonly InputType Type;
        public readonly char Char;
        public readonly Key Key;

        public Input(char c)
        {
            Type = InputType.Char;
            Char = c;
            Key = default;
        }
        
        public Input(Key key, bool pressed)
        {
            Type = pressed ? InputType.KeyPress : InputType.KeyRelease;
            Key = key;
            Char = '\0';
        }
        
        public static bool operator ==(Input input, Key key) => input.Type == InputType.KeyPress && input.Key == key;
        public static bool operator !=(Input input, Key key) => input.Type != InputType.KeyPress || input.Key != key;
        
        public static bool operator ==(Input input, char c) => input.Type == InputType.Char && input.Char == c;
        public static bool operator !=(Input input, char c) => input.Type != InputType.Char || input.Char != c;
    }

    public enum InputType
    {
        KeyPress,
        KeyRelease,
        Char,
    }
    
    public enum Key
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
        F18 = 129,
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

    private readonly struct InteractionState(InputState<bool> hovering, InputState<MouseButtons> buttons, InputState<IntVector2> cursorPosition)
    {
        public readonly InputState<bool> Hovering = hovering;
        public readonly InputState<MouseButtons> Buttons = buttons;
        public readonly InputState<IntVector2> CursorPosition = cursorPosition;
    }

    /// <summary>
    ///     True when a non-passthrough UI element consumed mouse hover, click, drag, or scroll input on the last built frame.
    /// </summary>
    public bool IsMouseCaptured { get; private set; }

    /// <summary>
    ///     True when a UI element currently holds keyboard focus.
    /// </summary>
    public bool IsKeyboardCaptured => _focusedID != null;

    private InputState<IntVector2> _cursor;
    private InputState<bool> _leftMouse;
    private InputState<bool> _rightMouse;
    private InputState<bool> _middleMouse;
    private Vector2 _scrollDelta;
    private string? _focusedID;
    private bool _focusExplicitlySetThisFrame;
    private string? _activeLeftID;
    private string? _activeRightID;
    private string? _activeMiddleID;
    private string? _scrolledElementID;
    private string? _pendingScrolledElementID;
    private bool _interactionConsumed;
    private bool _scrollConsumed;
    private bool _anyElementClicked;
    private readonly object _inputLock = new();
    private readonly List<Input> _inputBuffer = [];
    private Input[] _inputBufferSnapshot = [];
    private readonly HashSet<Key> _heldKeys = [];
    private readonly Dictionary<string, InteractionState> _interactionStates = [];
    private readonly Dictionary<string, TextLayout> _textLayoutCache = [];
    private readonly Dictionary<string, ElementBounds> _elementBoundsCache = [];
    private readonly HashSet<string> _updatedInteractionIDs = [];
    private readonly HashSet<string> _updatedTextLayoutIDs = [];
    private readonly HashSet<string> _updatedElementBoundsIDs = [];
    private readonly List<string> _removedIDs = [];
    
    public void UpdateMouse(int x, int y, MouseButtons downMouseButtons, float scrollDeltaX = 0f, float scrollDeltaY = 0f)
    {
        _cursor = new InputState<IntVector2>(previous: _cursor.Current, current: new IntVector2(x, y));
        _leftMouse = new InputState<bool>(previous: _leftMouse.Current, current: (downMouseButtons & MouseButtons.Left) == MouseButtons.Left);
        _rightMouse = new InputState<bool>(previous: _rightMouse.Current, current: (downMouseButtons & MouseButtons.Right) == MouseButtons.Right);
        _middleMouse = new InputState<bool>(previous: _middleMouse.Current, current: (downMouseButtons & MouseButtons.Middle) == MouseButtons.Middle);
        _scrollDelta = new Vector2(scrollDeltaX, scrollDeltaY);

        if (!_leftMouse.Current)
        {
            _activeLeftID = null;
        }

        if (!_rightMouse.Current)
        {
            _activeRightID = null;
        }

        if (!_middleMouse.Current)
        {
            _activeMiddleID = null;
        }
    }

    public void UpdateInputBuffer(IEnumerable<Input> buffer)
    {
        lock (_inputLock)
        {
            _inputBuffer.Clear();
            foreach (Input input in buffer)
            {
                _inputBuffer.Add(input);
                
                if (input.Type == InputType.KeyPress)
                {
                    _heldKeys.Add(input.Key);
                }
                else if (input.Type == InputType.KeyRelease)
                {
                    _heldKeys.Remove(input.Key);
                }

                //  On esc, unfocus any currently focused element.
                if (input == Key.Esc)
                {
                    _focusedID = null;
                }
            }

            _inputBufferSnapshot = _inputBuffer.Count > 0 ? _inputBuffer.ToArray() : [];
        }
    }
    
    internal IntVector2 GetCursorPosition() => _cursor.Current;
    internal IntVector2 GetCursorDelta() => _cursor.Current - _cursor.Previous;

    internal float GetScrollDelta() => _scrollDelta.Y;
    internal Vector2 GetScrollDelta2D() => _scrollDelta;

    internal bool IsScrolled(string id) => _scrolledElementID == id && (_scrollDelta.X != 0f || _scrollDelta.Y != 0f);
    internal float GetElementScrollDelta(string id) => _scrolledElementID == id ? _scrollDelta.Y : 0f;
    internal Vector2 GetElementScrollDelta2D(string id) => _scrolledElementID == id ? _scrollDelta : Vector2.Zero;

    internal bool IsLeftPressed() => IsPressed(_leftMouse);
    internal bool IsLeftReleased() => IsReleased(_leftMouse);
    internal bool IsLeftHeld() => IsHeld(_leftMouse);
    
    internal bool IsRightPressed() => IsPressed(_rightMouse);
    internal bool IsRightReleased() => IsReleased(_rightMouse);
    internal bool IsRightHeld() => IsHeld(_rightMouse);

    internal bool IsMiddlePressed() => IsPressed(_middleMouse);
    internal bool IsMiddleReleased() => IsReleased(_middleMouse);
    internal bool IsMiddleHeld() => IsHeld(_middleMouse);
    
    internal IntVector2 GetRelativeCursorPosition(string id)
    {
        if (!_interactionStates.TryGetValue(id, out InteractionState interactionState))
        {
            return default;
        }
        
        return interactionState.CursorPosition.Current;
    }

    internal bool IsClicked(string id, MouseButtons button = MouseButtons.Left) =>
        _interactionStates.TryGetValue(id, out InteractionState interactionState) && IsButtonPressed(interactionState.Buttons, button);

    internal bool IsReleased(string id, MouseButtons button = MouseButtons.Left) =>
        _interactionStates.TryGetValue(id, out InteractionState interactionState) && IsButtonReleased(interactionState.Buttons, button);

    internal bool IsHeld(string id, MouseButtons button = MouseButtons.Left) =>
        _interactionStates.TryGetValue(id, out InteractionState interactionState) && IsButtonHeld(interactionState.Buttons, button);

    internal bool IsHovering(string id) => _interactionStates.TryGetValue(id, out InteractionState interactionState) && interactionState.Hovering.Current;
    internal bool IsEntering(string id) => _interactionStates.TryGetValue(id, out InteractionState interactionState) && IsPressed(interactionState.Hovering);
    internal bool IsExiting(string id) => _interactionStates.TryGetValue(id, out InteractionState interactionState) && IsReleased(interactionState.Hovering);
    
    internal bool IsFocused(string id) => _focusedID == id;
    internal void Focus(string id)
    {
        _focusedID = id;
        _focusExplicitlySetThisFrame = true;
    }

    internal void Unfocus()
    {
        _focusedID = null;
        _focusExplicitlySetThisFrame = true;
    }
    
    internal bool IsPressed(Key key)
    {
        lock (_inputLock)
        {
            for (int i = _inputBuffer.Count - 1; i >= 0; i--)
            {
                Input input = _inputBuffer[i];
                if (input.Type > InputType.KeyRelease || input.Key != key)
                {
                    continue;
                }
                
                return input.Type == InputType.KeyPress;
            }

            return false;
        }
    }
    
    internal bool IsReleased(Key key)
    {
        lock (_inputLock)
        {
            for (int i = _inputBuffer.Count - 1; i >= 0; i--)
            {
                Input input = _inputBuffer[i];
                if (input.Type > InputType.KeyRelease || input.Key != key)
                {
                    continue;
                }
            
                return input.Type == InputType.KeyRelease;
            }

            return false;
        }
    }

    internal bool IsHeld(Key key)
    {
        lock (_inputLock)
        {
            return _heldKeys.Contains(key);
        }
    }
    
    internal IReadOnlyCollection<Input> GetInputBuffer()
    {
        lock (_inputLock)
        {
            return _inputBufferSnapshot;
        }
    }
    
    internal void EndUpdate()
    {
        //  If a click occurred outside any interactive element and focus wasn't explicitly set, clear focus.
        if (IsLeftPressed() && !_anyElementClicked && !_focusExplicitlySetThisFrame)
        {
            _focusedID = null;
        }

        //  Remove interaction states for elements that weren't rendered this frame
        if (_updatedInteractionIDs.Count < _interactionStates.Count)
        {
            _removedIDs.Clear();
            foreach (string id in _interactionStates.Keys)
            {
                if (!_updatedInteractionIDs.Contains(id))
                {
                    _removedIDs.Add(id);
                }
            }

            for (var i = 0; i < _removedIDs.Count; i++)
            {
                _interactionStates.Remove(_removedIDs[i]);
            }
        }
        _updatedInteractionIDs.Clear();

        //  Remove text layouts for elements that weren't rendered this frame
        if (_updatedTextLayoutIDs.Count < _textLayoutCache.Count)
        {
            _removedIDs.Clear();
            foreach (string id in _textLayoutCache.Keys)
            {
                if (!_updatedTextLayoutIDs.Contains(id))
                {
                    _removedIDs.Add(id);
                }
            }

            for (var i = 0; i < _removedIDs.Count; i++)
            {
                _textLayoutCache.Remove(_removedIDs[i]);
            }
        }
        _updatedTextLayoutIDs.Clear();

        //  Remove element bounds for elements that weren't rendered this frame
        if (_updatedElementBoundsIDs.Count < _elementBoundsCache.Count)
        {
            _removedIDs.Clear();
            foreach (string id in _elementBoundsCache.Keys)
            {
                if (!_updatedElementBoundsIDs.Contains(id))
                {
                    _removedIDs.Add(id);
                }
            }

            for (var i = 0; i < _removedIDs.Count; i++)
            {
                _elementBoundsCache.Remove(_removedIDs[i]);
            }
        }
        _updatedElementBoundsIDs.Clear();

        IsMouseCaptured = _interactionConsumed || _scrollConsumed;
        _scrolledElementID = _pendingScrolledElementID;
        _pendingScrolledElementID = null;
        _interactionConsumed = false;
        _scrollConsumed = false;
        _anyElementClicked = false;
        _focusExplicitlySetThisFrame = false;
    }
    
    internal void UpdateElementBounds(string id, IntRect rect, IntRect clipRect, IntVector2 contentSize)
    {
        if (_updatedElementBoundsIDs.Add(id))
        {
            _elementBoundsCache[id] = new ElementBounds(rect, clipRect, contentSize);
        }
    }

    internal void UpdateInteraction(string id, IntRect rect, IntRect clipRect, IntVector2 contentSize = default, bool scrollable = false)
    {
        UpdateElementBounds(id, rect, clipRect, contentSize);

        //  If a top-most element with the same ID already processed interactions this frame, do not overwrite it.
        if (!_updatedInteractionIDs.Add(id))
        {
            return;
        }

        IntVector2 cursorPos = _cursor.Current;

        int left = Math.Max(rect.Left, clipRect.Left);
        int top = Math.Max(rect.Top, clipRect.Top);
        int right = Math.Min(rect.Right, clipRect.Right);
        int bottom = Math.Min(rect.Bottom, clipRect.Bottom);

        bool insideClip = right > left
            && bottom > top
            && cursorPos.X >= left
            && cursorPos.X < right
            && cursorPos.Y >= top
            && cursorPos.Y < bottom;

        if (scrollable && insideClip && !_scrollConsumed)
        {
            _scrollConsumed = true;
            _pendingScrolledElementID = id;
        }

        bool hovering = !_interactionConsumed && insideClip;

        bool leftClicked = hovering && IsPressed(_leftMouse);
        bool rightClicked = hovering && IsPressed(_rightMouse);
        bool middleClicked = hovering && IsPressed(_middleMouse);

        if (leftClicked)
        {
            _activeLeftID = id;
        }

        if (rightClicked)
        {
            _activeRightID = id;
        }

        if (middleClicked)
        {
            _activeMiddleID = id;
        }

        bool leftInteracting = leftClicked || _activeLeftID == id && IsHeld(_leftMouse);
        bool rightInteracting = rightClicked || _activeRightID == id && IsHeld(_rightMouse);
        bool middleInteracting = middleClicked || _activeMiddleID == id && IsHeld(_middleMouse);

        var interactingButtons = MouseButtons.None;
        if (leftInteracting)
        {
            interactingButtons |= MouseButtons.Left;
        }

        if (rightInteracting)
        {
            interactingButtons |= MouseButtons.Right;
        }

        if (middleInteracting)
        {
            interactingButtons |= MouseButtons.Middle;
        }

        if (hovering || interactingButtons != MouseButtons.None)
        {
            _interactionConsumed = true;
        }

        int localCursorX = cursorPos.X - rect.Left;
        int localCursorY = cursorPos.Y - rect.Top;
        var localCursorPosition = new IntVector2(localCursorX, localCursorY);

        if (_interactionStates.TryGetValue(id, out InteractionState interactionState))
        {
            var hoveredState = new InputState<bool>(previous: interactionState.Hovering.Current, current: hovering);
            var buttonsState = new InputState<MouseButtons>(previous: interactionState.Buttons.Current, current: interactingButtons);
            var cursorPositionState = new InputState<IntVector2>(previous: interactionState.CursorPosition.Current, current: localCursorPosition);
            _interactionStates[id] = new InteractionState(hoveredState, buttonsState, cursorPositionState);
        }
        else
        {
            var hoveredState = new InputState<bool>(previous: false, current: hovering);
            var buttonsState = new InputState<MouseButtons>(previous: MouseButtons.None, current: interactingButtons);
            var cursorPositionState = new InputState<IntVector2>(previous: localCursorPosition, current: localCursorPosition);
            _interactionStates.Add(id, new InteractionState(hoveredState, buttonsState, cursorPositionState));
        }
        
        if (leftClicked)
        {
            _anyElementClicked = true;
            if (!_focusExplicitlySetThisFrame)
            {
                _focusedID = id;
            }
        }
    }

    internal void UpdateTextLayout(string id, TextLayout layout)
    {
        _updatedTextLayoutIDs.Add(id);
        _textLayoutCache[id] = layout;
    }

    internal TextLayout GetTextLayout(string id)
    {
        if (_textLayoutCache.TryGetValue(id, out TextLayout value))
        {
            return value;
        }
        
        return TextLayout.Empty;
    }

    internal ElementBounds GetElementBounds(string id)
    {
        if (_elementBoundsCache.TryGetValue(id, out ElementBounds value))
        {
            return value;
        }

        return ElementBounds.Empty;
    }

    private static bool IsPressed(InputState<bool> state) => !state.Previous && state.Current;
    
    private static bool IsReleased(InputState<bool> state) => state.Previous && !state.Current;
    
    private static bool IsHeld(InputState<bool> state) => state.Previous && state.Current;

    private static bool IsButtonPressed(InputState<MouseButtons> state, MouseButtons button) =>
        (state.Previous & button) == MouseButtons.None && (state.Current & button) == button;

    private static bool IsButtonReleased(InputState<MouseButtons> state, MouseButtons button) =>
        (state.Previous & button) == button && (state.Current & button) == MouseButtons.None;

    private static bool IsButtonHeld(InputState<MouseButtons> state, MouseButtons button) =>
        (state.Previous & button) == button && (state.Current & button) == button;
}
