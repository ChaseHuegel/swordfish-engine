using System.Collections.Concurrent;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Events;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Client-side owner of mouse capture, the window-focus lock, the <see cref="PlayerMovedEvent"/>
/// emission, and the client's <see cref="SharedPlayerMotionStep"/>. The step is bound to the shared
/// physics + world store lazily on the first tick (never via DI) so constructing the ECS world never
/// resolves the world store, which would recurse during container build. This system exists only where
/// the simulator meets the window, so the capture, motion statistics, and prediction survive the removal
/// of the client-authoritative controller path.
///
/// Cursor movement is captured on the high-frequency window-update path (per render frame) into a queue,
/// because <see cref="IInputService.CursorDelta"/> is a transient per-frame value that sampling at the
/// slower ECS tick rate would miss entirely.
/// </summary>
internal sealed class ClientPlayerMotionProcessor : IEntitySystem
{
    /// <summary>Filled on the window thread per render frame; drained by <see cref="Systems.ClientInputSystem"/> on the ECS thread.</summary>
    public readonly ConcurrentQueue<Vector2> CursorUpdates = new();

    private readonly IInputService _inputService;
    private readonly IPhysics _physics;
    private readonly EventInvoker<PlayerMovedEvent> _playerMovedEvent;

    private DataStore? _store;
    private bool _inputEnabled;
    private bool _savedMouseLookState;
    private SharedPlayerMotionStep? _step;

    public SharedPlayerMotionStep? Step => _step;

    public ClientPlayerMotionProcessor(
        in IInputService inputService,
        in IWindowContext windowContext,
        in IPhysics physics,
        in EventInvoker<PlayerMovedEvent> playerMovedEvent
    ) {
        _inputService = inputService;
        _physics = physics;
        _playerMovedEvent = playerMovedEvent;

        windowContext.Update += OnWindowUpdate;
        windowContext.Focused += OnWindowFocused;
        windowContext.Unfocused += OnWindowUnfocused;
    }

    private void OnWindowUpdate(double delta)
    {
        if (WaywardBeyond.IsPlaying())
        {
            CursorUpdates.Enqueue(_inputService.CursorDelta);
        }
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
        _store ??= store;
        _step ??= new SharedPlayerMotionStep(store, _physics, ResolveCommand);

        PlayerMovedAction action = new() { Owner = this };
        store.Query<PlayerComponent, TransformComponent, PlayerMovedAction>(delta, ref action);
    }

    private bool ResolveCommand(int entity, uint simTick, out InputComponent command)
    {
        //  Client prediction applies the newest locally sampled input on the entity.
        DataStore store = _store!;
        return store.TryGet(entity, out command);
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