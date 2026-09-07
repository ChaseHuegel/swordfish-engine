using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Events;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Client-side owner of mouse capture, the window-focus lock, and the <see cref="PlayerMovedEvent"/>
/// emission. The actual player motion lives in <see cref="Shared.Gameplay.SharedPlayerMotionStep"/>
/// (driven per physics step); this system exists only where the simulator meets the window, so the
/// capture and movement statistics survive the removal of the client-authoritative controller path.
/// </summary>
internal sealed class ClientPlayerMotionProcessor : IEntitySystem
{
    private readonly IInputService _inputService;
    private readonly EventInvoker<PlayerMovedEvent> _playerMovedEvent;

    private bool _inputEnabled;
    private bool _savedMouseLookState;

    public ClientPlayerMotionProcessor(
        in IInputService inputService,
        in IWindowContext windowContext,
        in EventInvoker<PlayerMovedEvent> playerMovedEvent
    ) {
        _inputService = inputService;
        _playerMovedEvent = playerMovedEvent;

        windowContext.Focused += OnWindowFocused;
        windowContext.Unfocused += OnWindowUnfocused;
    }

    public void SetInputEnabled(bool enabled)
    {
        if (_inputEnabled == enabled)
        {
            return;
        }

        _inputEnabled = enabled;
        if (enabled)
        {
            _inputService.CursorOptions = CursorOptions.Hidden | CursorOptions.Locked;
            _ = _inputService.CursorDelta;  //  ! HACKY Consume delta state
        }
        else
        {
            _inputService.CursorOptions = CursorOptions.None;
        }
    }

    private void OnWindowFocused()
    {
        SetInputEnabled(_savedMouseLookState);
    }

    private void OnWindowUnfocused()
    {
        _savedMouseLookState = _inputEnabled;
        SetInputEnabled(false);
    }

    public void Tick(float delta, DataStore store)
    {
        PlayerMovedAction action = new() { Owner = this };
        store.Query<PlayerComponent, TransformComponent, PlayerMovedAction>(delta, ref action);
    }

    private struct PlayerMovedAction : IForEach<PlayerComponent, TransformComponent>
    {
        public ClientPlayerMotionProcessor Owner;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player, in TransformComponent transform)
        {
            Owner._playerMovedEvent.Invoke(new PlayerMovedEvent(transform.Position));
        }
    }
}