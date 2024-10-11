using Swordfish.Library.Types;

namespace Swordfish.Settings;

public record GizmoSettings
{
    public DataBinding<bool> Transforms { get; } = new();
    public DataBinding<bool> Physics { get; } = new();
}