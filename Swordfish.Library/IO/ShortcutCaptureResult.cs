#nullable enable

namespace Swordfish.Library.IO;

/// <summary>Represents the outcome of capturing a single shortcut input.</summary>
public readonly struct ShortcutCaptureResult
{
    /// <summary>Whether the capture was cancelled before an input was chosen.</summary>
    public readonly bool Cancelled;

    /// <summary>The captured binding when <see cref="Cancelled"/> is false.</summary>
    public readonly ShortcutBinding Binding;

    /// <summary>Initializes a new capture result.</summary>
    public ShortcutCaptureResult(bool cancelled, ShortcutBinding binding)
    {
        Cancelled = cancelled;
        Binding = binding;
    }

    /// <summary>Creates a completed capture result for <paramref name="binding"/>.</summary>
    public static ShortcutCaptureResult FromBinding(ShortcutBinding binding) => new(false, binding);

    /// <summary>Creates a cancelled capture result.</summary>
    public static ShortcutCaptureResult FromCancelled() => new(true, ShortcutBinding.Unbound);
}
