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

    private InputState<Position> _cursor;
    private InputState<bool> _leftMouse;
    private InputState<bool> _rightMouse;
    private InputState<bool> _middleMouse;
    private readonly Dictionary<string, InputState<bool>> _buttons = [];
    
    public void Update(int x, int y, MouseButtons downMouseButtons)
    {
        _cursor = new InputState<Position>(previous: _cursor.Current, current: new Position(x, y));
        _leftMouse = new InputState<bool>(previous: _leftMouse.Current, current: (downMouseButtons & MouseButtons.Left) == MouseButtons.Left);
        _rightMouse = new InputState<bool>(previous: _rightMouse.Current, current: (downMouseButtons & MouseButtons.Right) == MouseButtons.Right);
        _middleMouse = new InputState<bool>(previous: _middleMouse.Current, current: (downMouseButtons & MouseButtons.Middle) == MouseButtons.Middle);
    }

    internal bool WasButtonClicked(string id)
    {
        return _buttons.TryGetValue(id, out InputState<bool> buttonState) && IsPressed(buttonState);
    }
    
    internal void UpdateButton(string id, IntRect rect)
    {
        Position cursorPos = _cursor.Current;
        bool inRect = cursorPos.X >= rect.Left &&
                      cursorPos.X <= rect.Right &&
                      cursorPos.Y >= rect.Top &&
                      cursorPos.Y <= rect.Bottom;

        bool rectClicked = inRect && IsPressed(_leftMouse);

        if (_buttons.TryGetValue(id, out InputState<bool> buttonState))
        {
            _buttons[id] = new InputState<bool>(previous: buttonState.Current, current: rectClicked);
        }
        else
        {
            _buttons.Add(id, new InputState<bool>(previous: false, current: rectClicked));
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