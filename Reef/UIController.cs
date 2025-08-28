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

    private readonly struct InteractionState(InputState<bool> hovering, InputState<bool> button)
    {
        public readonly InputState<bool> Hovering = hovering;
        public readonly InputState<bool> Button = button;
    }

    private InputState<Position> _cursor;
    private InputState<bool> _leftMouse;
    private InputState<bool> _rightMouse;
    private InputState<bool> _middleMouse;
    private readonly Dictionary<string, InteractionState> _interactionStates = [];
    
    public void UpdateMouse(int x, int y, MouseButtons downMouseButtons)
    {
        _cursor = new InputState<Position>(previous: _cursor.Current, current: new Position(x, y));
        _leftMouse = new InputState<bool>(previous: _leftMouse.Current, current: (downMouseButtons & MouseButtons.Left) == MouseButtons.Left);
        _rightMouse = new InputState<bool>(previous: _rightMouse.Current, current: (downMouseButtons & MouseButtons.Right) == MouseButtons.Right);
        _middleMouse = new InputState<bool>(previous: _middleMouse.Current, current: (downMouseButtons & MouseButtons.Middle) == MouseButtons.Middle);
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

    internal void UpdateInteraction(string id, IntRect rect)
    {
        Position cursorPos = _cursor.Current;
        bool hovered = rect.Contains(cursorPos.X, cursorPos.Y);
        bool clicked = hovered && IsPressed(_leftMouse);

        if (_interactionStates.TryGetValue(id, out InteractionState interactionState))
        {
            var hoveredState = new InputState<bool>(previous: interactionState.Hovering.Current, current: hovered);
            var clickedState = new InputState<bool>(previous: interactionState.Button.Current, current: clicked);
            _interactionStates[id] = new InteractionState(hoveredState, clickedState);
        }
        else
        {
            var hoveredState = new InputState<bool>(previous: false, current: hovered);
            var clickedState = new InputState<bool>(previous: false, current: clicked);
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