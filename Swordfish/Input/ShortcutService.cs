using Microsoft.Extensions.Logging;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace Swordfish.Input;

/// <summary>Dispatches input shortcuts, tracks live binding overrides, and captures single-input rebinds.</summary>
public class ShortcutService : IShortcutService, IDisposable
{
    private sealed class RegisteredShortcut(
        string id,
        string name,
        string? category,
        string? description,
        ShortcutBinding defaultBinding,
        ShortcutBinding currentBinding,
        bool isFixed,
        bool ignoreUnboundModifiers,
        string? sharedBindingGroup
    )
    {
        public readonly string Id = id;
        public readonly string Name = name;
        public readonly string? Category = category;
        public readonly string? Description = description;
        public readonly ShortcutBinding DefaultBinding = defaultBinding;
        public readonly bool Fixed = isFixed;
        public readonly bool IgnoreUnboundModifiers = ignoreUnboundModifiers;
        public readonly string? SharedBindingGroup = sharedBindingGroup;
        public readonly List<ShortcutHandler> Handlers = [];
        public ShortcutBinding CurrentBinding = currentBinding;
    }

    private sealed class ShortcutHandler(Func<bool>? isEnabled, Action? action, Action? released)
    {
        public readonly Func<bool>? IsEnabled = isEnabled;
        public readonly Action? Action = action;
        public readonly Action? Released = released;
        public bool PendingRelease;
        public ShortcutBinding PendingBinding;
    }

    private readonly struct PendingAction(string shortcutId, Action callback)
    {
        public readonly string ShortcutId = shortcutId;
        public readonly Action Callback = callback;
    }

    private readonly struct PressCandidate(
        string shortcutId,
        ShortcutHandler handler,
        ShortcutBinding binding,
        bool isExact
    )
    {
        public readonly string ShortcutId = shortcutId;
        public readonly ShortcutHandler Handler = handler;
        public readonly ShortcutBinding Binding = binding;
        public readonly bool IsExact = isExact;
    }

    private readonly IInputService _inputService;
    private readonly ILogger _logger;
    private readonly object _sync = new();
    private readonly Dictionary<string, RegisteredShortcut> _registeredShortcuts = new(StringComparer.Ordinal);
    private readonly List<RegisteredShortcut> _shortcutOrder = [];
    private readonly Dictionary<string, ShortcutBinding> _overrides = new(StringComparer.Ordinal);

    private Action<ShortcutCaptureResult>? _captureCallback;
    private Key _capturedModifierKey = Key.None;

    /// <summary>Initializes a new instance of the <see cref="ShortcutService"/> class.</summary>
    public ShortcutService(IInputService inputService, ILogger logger)
    {
        _inputService = inputService;
        _logger = logger;

        _inputService.KeyPressed += OnKeyPressed;
        _inputService.KeyReleased += OnKeyReleased;
        _inputService.Clicked += OnMouseClicked;
        _inputService.MouseReleased += OnMouseReleased;
    }

    /// <inheritdoc/>
    public event Action? BindingsChanged;

    /// <inheritdoc/>
    public bool IsCapturing
    {
        get
        {
            lock (_sync)
            {
                return _captureCallback != null;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _inputService.KeyPressed -= OnKeyPressed;
        _inputService.KeyReleased -= OnKeyReleased;
        _inputService.Clicked -= OnMouseClicked;
        _inputService.MouseReleased -= OnMouseReleased;
    }

    /// <inheritdoc/>
    public bool RegisterShortcut(Shortcut shortcut)
    {
        string id = shortcut.GetId();
        ShortcutBinding defaultBinding = shortcut.GetBinding();
        bool overrideApplied;

        lock (_sync)
        {
            if (_registeredShortcuts.ContainsKey(id))
            {
                return false;
            }

            ShortcutBinding activeBinding = defaultBinding;
            if (shortcut.Fixed)
            {
                _overrides.Remove(id);
            }
            else if (_overrides.TryGetValue(id, out ShortcutBinding overrideBinding))
            {
                activeBinding = overrideBinding;
                if (overrideBinding == defaultBinding)
                {
                    _overrides.Remove(id);
                }
            }

            overrideApplied = !shortcut.Fixed && activeBinding != defaultBinding;

            var registration = new RegisteredShortcut(
                id,
                shortcut.Name,
                shortcut.Category,
                shortcut.Description,
                defaultBinding,
                activeBinding,
                shortcut.Fixed,
                shortcut.IgnoreUnboundModifiers,
                shortcut.SharedBindingGroup
            );

            if (shortcut.Action != null || shortcut.Released != null)
            {
                registration.Handlers.Add(new ShortcutHandler(shortcut.IsEnabled, shortcut.Action, shortcut.Released));
            }

            _registeredShortcuts.Add(id, registration);
            _shortcutOrder.Add(registration);
        }

        if (overrideApplied)
        {
            BindingsChanged?.Invoke();
        }

        return true;
    }

    /// <inheritdoc/>
    public bool AddHandler(string id, Func<bool>? isEnabled, Action action, Action? released = null)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        lock (_sync)
        {
            if (!_registeredShortcuts.TryGetValue(id, out RegisteredShortcut? registration))
            {
                return false;
            }

            registration.Handlers.Add(new ShortcutHandler(isEnabled, action, released));
            return true;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<ShortcutInfo> GetShortcuts()
    {
        lock (_sync)
        {
            var infos = new ShortcutInfo[_shortcutOrder.Count];
            for (var i = 0; i < _shortcutOrder.Count; i++)
            {
                RegisteredShortcut registration = _shortcutOrder[i];
                infos[i] = new ShortcutInfo(
                    registration.Id,
                    registration.Name,
                    registration.Category,
                    registration.Description,
                    registration.DefaultBinding,
                    registration.CurrentBinding,
                    !registration.Fixed,
                    registration.SharedBindingGroup
                );
            }

            return infos;
        }
    }

    /// <inheritdoc/>
    public Result<ShortcutBinding> GetBinding(string id)
    {
        lock (_sync)
        {
            if (_registeredShortcuts.TryGetValue(id, out RegisteredShortcut? registration))
            {
                return Result<ShortcutBinding>.FromSuccess(registration.CurrentBinding);
            }

            return Result<ShortcutBinding>.FromFailure($"Unknown shortcut \"{id}\".");
        }
    }

    /// <inheritdoc/>
    public bool IsHeld(string id)
    {
        ShortcutBinding binding;
        lock (_sync)
        {
            if (!_registeredShortcuts.TryGetValue(id, out RegisteredShortcut? registration))
            {
                return false;
            }

            binding = registration.CurrentBinding;
        }

        if (!binding.IsBound)
        {
            return false;
        }

        bool triggerHeld = binding.IsKey
            ? _inputService.IsKeyHeld(binding.Key)
            : _inputService.IsMouseHeld(binding.MouseButton);
        if (!triggerHeld)
        {
            return false;
        }

        if (binding.Modifiers == ShortcutModifiers.None)
        {
            return true;
        }

        if ((binding.Modifiers & ShortcutModifiers.Control) != 0
            && (!binding.IsKey || binding.Key != Key.Control)
            && !_inputService.IsKeyHeld(Key.Control))
        {
            return false;
        }

        if ((binding.Modifiers & ShortcutModifiers.Shift) != 0
            && (!binding.IsKey || binding.Key != Key.Shift)
            && !_inputService.IsKeyHeld(Key.Shift))
        {
            return false;
        }

        if ((binding.Modifiers & ShortcutModifiers.Alt) != 0
            && (!binding.IsKey || binding.Key != Key.Alt)
            && !_inputService.IsKeyHeld(Key.Alt))
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public Result Rebind(string id, ShortcutBinding binding)
    {
        bool changed;
        lock (_sync)
        {
            if (!_registeredShortcuts.TryGetValue(id, out RegisteredShortcut? registration))
            {
                return Result.FromFailure($"Unknown shortcut \"{id}\".");
            }

            if (registration.Fixed)
            {
                return Result.FromFailure($"Shortcut \"{id}\" cannot be rebound.");
            }

            changed = registration.CurrentBinding != binding;
            registration.CurrentBinding = binding;

            if (binding == registration.DefaultBinding)
            {
                _overrides.Remove(id);
            }
            else
            {
                _overrides[id] = binding;
            }
        }

        if (changed)
        {
            BindingsChanged?.Invoke();
        }

        return Result.FromSuccess();
    }

    /// <inheritdoc/>
    public Result ResetBinding(string id)
    {
        bool changed;
        lock (_sync)
        {
            if (!_registeredShortcuts.TryGetValue(id, out RegisteredShortcut? registration))
            {
                return Result.FromFailure($"Unknown shortcut \"{id}\".");
            }

            if (registration.Fixed)
            {
                return Result.FromFailure($"Shortcut \"{id}\" cannot be reset.");
            }

            changed = registration.CurrentBinding != registration.DefaultBinding;
            registration.CurrentBinding = registration.DefaultBinding;
            _overrides.Remove(id);
        }

        if (changed)
        {
            BindingsChanged?.Invoke();
        }

        return Result.FromSuccess();
    }

    /// <inheritdoc/>
    public void ResetAllBindings()
    {
        var changed = false;
        lock (_sync)
        {
            for (var i = 0; i < _shortcutOrder.Count; i++)
            {
                RegisteredShortcut registration = _shortcutOrder[i];
                if (registration.Fixed)
                {
                    continue;
                }

                if (registration.CurrentBinding != registration.DefaultBinding)
                {
                    registration.CurrentBinding = registration.DefaultBinding;
                    changed = true;
                }
            }

            _overrides.Clear();
        }

        if (changed)
        {
            BindingsChanged?.Invoke();
        }
    }

    /// <inheritdoc/>
    public void ApplyOverrides(IReadOnlyDictionary<string, ShortcutBinding> overrides)
    {
        var changed = false;
        lock (_sync)
        {
            foreach ((string id, ShortcutBinding binding) in overrides)
            {
                if (_registeredShortcuts.TryGetValue(id, out RegisteredShortcut? registration))
                {
                    if (registration.Fixed)
                    {
                        _overrides.Remove(id);
                        continue;
                    }

                    if (registration.CurrentBinding != binding)
                    {
                        registration.CurrentBinding = binding;
                        changed = true;
                    }

                    if (binding == registration.DefaultBinding)
                    {
                        _overrides.Remove(id);
                    }
                    else
                    {
                        _overrides[id] = binding;
                    }
                }
                else
                {
                    _overrides[id] = binding;
                }
            }
        }

        if (changed)
        {
            BindingsChanged?.Invoke();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, ShortcutBinding> GetOverrides()
    {
        lock (_sync)
        {
            return new Dictionary<string, ShortcutBinding>(_overrides, StringComparer.Ordinal);
        }
    }

    /// <inheritdoc/>
    public void BeginCapture(Action<ShortcutCaptureResult> onCompleted)
    {
        if (onCompleted == null)
        {
            throw new ArgumentNullException(nameof(onCompleted));
        }

        Action<ShortcutCaptureResult>? previous;
        lock (_sync)
        {
            previous = _captureCallback;
            _captureCallback = onCompleted;
            _capturedModifierKey = Key.None;
        }

        previous?.Invoke(ShortcutCaptureResult.FromCancelled());
    }

    /// <inheritdoc/>
    public void CancelCapture()
    {
        Action<ShortcutCaptureResult>? previous;
        lock (_sync)
        {
            previous = _captureCallback;
            _captureCallback = null;
            _capturedModifierKey = Key.None;
        }

        previous?.Invoke(ShortcutCaptureResult.FromCancelled());
    }

    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        ShortcutModifiers modifiers = GetHeldModifiers(e.Key);

        Action<ShortcutCaptureResult>? captureCallback = null;
        ShortcutCaptureResult captureResult = default;
        List<PressCandidate>? candidates = null;

        lock (_sync)
        {
            if (_captureCallback != null)
            {
                if (e.Key == Key.None)
                {
                    return;
                }

                if (e.Key == Key.Esc)
                {
                    captureCallback = _captureCallback;
                    _captureCallback = null;
                    _capturedModifierKey = Key.None;
                    captureResult = ShortcutCaptureResult.FromCancelled();
                }
                else if (IsModifierKey(e.Key))
                {
                    _capturedModifierKey = e.Key;
                    return;
                }
                else
                {
                    captureCallback = _captureCallback;
                    _captureCallback = null;
                    _capturedModifierKey = Key.None;
                    captureResult = ShortcutCaptureResult.FromBinding(new ShortcutBinding(e.Key, modifiers));
                }
            }
            else if (e.Key != Key.None)
            {
                candidates = CollectPressCandidates(e.Key, MouseButton.Unknown, modifiers);
            }
        }

        if (captureCallback != null)
        {
            captureCallback.Invoke(captureResult);
            return;
        }

        DispatchPressCandidates(candidates);
    }

    private void OnKeyReleased(object? sender, KeyEventArgs e)
    {
        ShortcutModifiers modifiers = GetHeldModifiers(e.Key);

        Action<ShortcutCaptureResult>? captureCallback = null;
        ShortcutCaptureResult captureResult = default;
        List<PendingAction>? actions = null;

        lock (_sync)
        {
            if (_captureCallback != null && _capturedModifierKey == e.Key && e.Key != Key.None)
            {
                captureCallback = _captureCallback;
                _captureCallback = null;
                _capturedModifierKey = Key.None;
                captureResult = ShortcutCaptureResult.FromBinding(new ShortcutBinding(e.Key, modifiers));
            }
            else if (e.Key != Key.None)
            {
                actions = CollectReleaseActions(e.Key, MouseButton.Unknown);
            }
        }

        if (captureCallback != null)
        {
            captureCallback.Invoke(captureResult);
            return;
        }

        InvokeActions(actions, isRelease: true);
    }

    private void OnMouseClicked(object? sender, ClickedEventArgs e)
    {
        ShortcutModifiers modifiers = GetHeldModifiers(Key.None);

        Action<ShortcutCaptureResult>? captureCallback = null;
        ShortcutCaptureResult captureResult = default;
        List<PressCandidate>? candidates = null;

        lock (_sync)
        {
            if (_captureCallback != null)
            {
                if (e.MouseButton == MouseButton.Unknown)
                {
                    return;
                }

                captureCallback = _captureCallback;
                _captureCallback = null;
                _capturedModifierKey = Key.None;
                captureResult = ShortcutCaptureResult.FromBinding(new ShortcutBinding(e.MouseButton, modifiers));
            }
            else if (e.MouseButton != MouseButton.Unknown)
            {
                candidates = CollectPressCandidates(Key.None, e.MouseButton, modifiers);
            }
        }

        if (captureCallback != null)
        {
            captureCallback.Invoke(captureResult);
            return;
        }

        DispatchPressCandidates(candidates);
    }

    private void OnMouseReleased(object? sender, ClickedEventArgs e)
    {
        List<PendingAction>? actions;
        lock (_sync)
        {
            if (e.MouseButton == MouseButton.Unknown)
            {
                return;
            }

            actions = CollectReleaseActions(Key.None, e.MouseButton);
        }

        InvokeActions(actions, isRelease: true);
    }

    private List<PressCandidate>? CollectPressCandidates(Key key, MouseButton mouseButton, ShortcutModifiers modifiers)
    {
        List<PressCandidate>? candidates = null;

        for (var i = 0; i < _shortcutOrder.Count; i++)
        {
            RegisteredShortcut registration = _shortcutOrder[i];
            ShortcutBinding binding = registration.CurrentBinding;
            if (!MatchesTrigger(in binding, key, mouseButton))
            {
                continue;
            }

            bool isExact = binding.Modifiers == modifiers;
            if (!isExact && (!registration.IgnoreUnboundModifiers || (modifiers & binding.Modifiers) != binding.Modifiers))
            {
                continue;
            }

            CollectCandidateHandlers(registration, binding, isExact, ref candidates);
        }

        return candidates;
    }

    private static void CollectCandidateHandlers(
        RegisteredShortcut registration,
        ShortcutBinding binding,
        bool isExact,
        ref List<PressCandidate>? candidates
    )
    {
        for (var j = 0; j < registration.Handlers.Count; j++)
        {
            ShortcutHandler handler = registration.Handlers[j];
            if (handler.Action == null && handler.Released == null)
            {
                continue;
            }

            candidates ??= [];
            candidates.Add(new PressCandidate(registration.Id, handler, binding, isExact));
        }
    }

    private void DispatchPressCandidates(List<PressCandidate>? candidates)
    {
        if (candidates == null)
        {
            return;
        }

        List<PressCandidate>? exactMatches = null;
        List<PressCandidate>? relaxedMatches = null;

        for (var i = 0; i < candidates.Count; i++)
        {
            PressCandidate candidate = candidates[i];
            if (!IsHandlerEnabled(candidate.ShortcutId, candidate.Handler))
            {
                continue;
            }

            if (candidate.IsExact)
            {
                exactMatches ??= [];
                exactMatches.Add(candidate);
            }
            else
            {
                relaxedMatches ??= [];
                relaxedMatches.Add(candidate);
            }
        }

        List<PressCandidate>? winners = exactMatches != null ? exactMatches : relaxedMatches;
        if (winners == null)
        {
            return;
        }

        List<PendingAction>? actions = null;
        lock (_sync)
        {
            for (var i = 0; i < winners.Count; i++)
            {
                PressCandidate match = winners[i];
                match.Handler.PendingRelease = true;
                match.Handler.PendingBinding = match.Binding;

                if (match.Handler.Action != null)
                {
                    actions ??= [];
                    actions.Add(new PendingAction(match.ShortcutId, match.Handler.Action));
                }
            }
        }

        InvokeActions(actions, isRelease: false);
    }

    private bool IsHandlerEnabled(string shortcutId, ShortcutHandler handler)
    {
        if (handler.IsEnabled == null)
        {
            return true;
        }

        try
        {
            return handler.IsEnabled.Invoke();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Caught an exception trying to evaluate IsEnabled for shortcut {shortcut}.",
                shortcutId
            );
            return false;
        }
    }

    private List<PendingAction>? CollectReleaseActions(Key key, MouseButton mouseButton)
    {
        List<PendingAction>? actions = null;
        for (var i = 0; i < _shortcutOrder.Count; i++)
        {
            RegisteredShortcut registration = _shortcutOrder[i];
            for (var j = 0; j < registration.Handlers.Count; j++)
            {
                ShortcutHandler handler = registration.Handlers[j];
                if (!handler.PendingRelease || !MatchesTrigger(in handler.PendingBinding, key, mouseButton))
                {
                    continue;
                }

                handler.PendingRelease = false;
                if (handler.Released != null)
                {
                    actions ??= [];
                    actions.Add(new PendingAction(registration.Id, handler.Released));
                }
            }
        }

        return actions;
    }

    private static bool MatchesTrigger(in ShortcutBinding binding, Key key, MouseButton mouseButton)
    {
        return key != Key.None
            ? binding.IsKey && binding.Key == key
            : binding.IsMouseButton && binding.MouseButton == mouseButton;
    }

    private void InvokeActions(List<PendingAction>? actions, bool isRelease)
    {
        if (actions == null)
        {
            return;
        }

        for (var i = 0; i < actions.Count; i++)
        {
            PendingAction pending = actions[i];
            Result<Exception> invokeResult = Safe.Invoke(pending.Callback);
            if (!invokeResult)
            {
                _logger.LogError(
                    invokeResult.Value,
                    "Caught an exception trying to invoke {kind} action {action} for shortcut {shortcut}.",
                    isRelease ? "released" : "pressed",
                    pending.Callback.Method,
                    pending.ShortcutId
                );
            }
        }
    }

    private ShortcutModifiers GetHeldModifiers(Key pressedKey)
    {
        var modifiers = ShortcutModifiers.None;

        if (pressedKey != Key.Control && _inputService.IsKeyHeld(Key.Control))
        {
            modifiers |= ShortcutModifiers.Control;
        }

        if (pressedKey != Key.Shift && _inputService.IsKeyHeld(Key.Shift))
        {
            modifiers |= ShortcutModifiers.Shift;
        }

        if (pressedKey != Key.Alt && _inputService.IsKeyHeld(Key.Alt))
        {
            modifiers |= ShortcutModifiers.Alt;
        }

        return modifiers;
    }

    private static bool IsModifierKey(Key key)
    {
        return key == Key.Control || key == Key.Shift || key == Key.Alt;
    }
}
