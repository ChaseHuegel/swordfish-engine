using Swordfish.Library.Types;

namespace Swordfish.Settings;

public record RenderSettings
{
    public DataBinding<bool> Wireframe { get; } = new();
    public DataBinding<bool> HideMeshes { get; } = new();
}