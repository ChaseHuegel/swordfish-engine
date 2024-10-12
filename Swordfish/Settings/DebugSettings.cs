using Swordfish.Library.Types;

namespace Swordfish.Settings;

public record DebugSettings
{
    public GizmoSettings Gizmos { get; } = new();

    public DataBinding<bool> Stats { get; } = new();
}
