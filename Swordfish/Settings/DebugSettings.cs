using Swordfish.Library.Types;

namespace Swordfish.Settings;

public record DebugSettings
{
    public GizmoSettings Gizmos { get; } = new();

    public DataBinding<bool> Stats { get; } = new();
    
    public DataBinding<bool> UI { get; } = new();

    /// <summary>Writes the blurred AO buffer to the screen instead of the scene.</summary>
    public DataBinding<bool> SAO { get; } = new();

    /// <summary>While the SAO debug view is on, skips the blur and shows the raw pass output.</summary>
    public DataBinding<bool> SAORaw { get; } = new();

    /// <summary>While the SAO debug view is on, shows the depth-edge magnitude instead of AO.</summary>
    public DataBinding<bool> SAODepth { get; } = new();
}
