#nullable enable
using System;
using System.Collections.Generic;
using Swordfish.Library.Util;

namespace Swordfish.Library.IO;

/// <summary>Manages registered input shortcuts, live bindings, hold queries, and input capture.</summary>
public interface IShortcutService
{
    /// <summary>Raised when any registered shortcut binding changes.</summary>
    event Action? BindingsChanged;

    /// <summary>Gets whether the service is currently capturing a single input binding.</summary>
    bool IsCapturing { get; }

    /// <summary>Registers a shortcut definition and returns false when its id already exists.</summary>
    bool RegisterShortcut(Shortcut shortcut);

    /// <summary>Attaches an additional handler to an already registered shortcut id.</summary>
    bool AddHandler(string id, Func<bool>? isEnabled, Action action, Action? released = null);

    /// <summary>Returns a snapshot of all registered shortcuts in registration order.</summary>
    IReadOnlyList<ShortcutInfo> GetShortcuts();

    /// <summary>Gets the current binding for the shortcut with <paramref name="id"/>.</summary>
    Result<ShortcutBinding> GetBinding(string id);

    /// <summary>Returns whether the shortcut with <paramref name="id"/> is currently held.</summary>
    bool IsHeld(string id);

    /// <summary>Updates the live binding for the rebindable shortcut with <paramref name="id"/>.</summary>
    Result Rebind(string id, ShortcutBinding binding);

    /// <summary>Restores the shipped default binding for the shortcut with <paramref name="id"/>.</summary>
    Result ResetBinding(string id);

    /// <summary>Restores all rebindable shortcuts to their shipped default bindings.</summary>
    void ResetAllBindings();

    /// <summary>Applies persisted binding overrides, including for shortcuts registered later.</summary>
    void ApplyOverrides(IReadOnlyDictionary<string, ShortcutBinding> overrides);

    /// <summary>Returns a copy of all non-default binding overrides.</summary>
    IReadOnlyDictionary<string, ShortcutBinding> GetOverrides();

    /// <summary>Begins capturing one keyboard or mouse input without dispatching shortcuts.</summary>
    void BeginCapture(Action<ShortcutCaptureResult> onCompleted);

    /// <summary>Cancels any active input capture.</summary>
    void CancelCapture();
}
