using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace Swordfish.Settings;

public sealed class RenderSettings : Config<RenderSettings>
{
    public DataBinding<AntiAliasing> AntiAliasing { get; private set; } = new(Settings.AntiAliasing.MSAA);
    
    public DataBinding<int> Framerate { get; private set; } = new();
    public DataBinding<bool> VSync { get; private set; } = new();

    public DataBinding<bool> Wireframe { get; private set; } = new();
    public DataBinding<bool> HideMeshes { get; private set; } = new();
}