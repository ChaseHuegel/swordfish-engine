using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.Library.Types;
using Swordfish.Settings;

namespace WaywardBeyond.Client.Core.Configuration;

internal sealed class SettingsManager : IAutoActivate
{
    private readonly ControlSettings _controlSettings;
    private readonly WindowSettings _windowSettings;
    private readonly RenderSettings _renderSettings;
    
    public SettingsManager(
        in IWindowContext windowContext,
        in ControlSettings controlSettings,
        in WindowSettings windowSettings,
        in RenderSettings renderSettings
    ) {
        _controlSettings = controlSettings;
        _windowSettings = windowSettings;
        _renderSettings = renderSettings;

        windowContext.Closed += OnWindowClosed;
        ApplySettings();
    }
    
    public void ApplySettings()
    {
        _windowSettings.Save();
        _renderSettings.Save();
        _controlSettings.Save();
    }

    private void OnWindowClosed()
    {
        ApplySettings();
    }
}