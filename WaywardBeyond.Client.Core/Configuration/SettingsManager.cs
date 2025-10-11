using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.Library.Types;
using Swordfish.Settings;

namespace WaywardBeyond.Client.Core.Configuration;

internal sealed class SettingsManager : IAutoActivate
{
    private readonly Settings _settings;
    private readonly WindowSettings _windowSettings;
    
    public SettingsManager(in IWindowContext windowContext, in Settings settings, in WindowSettings windowSettings)
    {
        _settings = settings;
        _windowSettings = windowSettings;

        windowContext.Closed += OnWindowClosed;
        settings.Display.Fullscreen.Changed += OnFullscreenChanged;
        ApplySettings();
    }
    
    public void ApplySettings()
    {
        ApplyFullscreen(_settings.Display.Fullscreen);
        
        _settings.Save();
    }

    private void OnWindowClosed()
    {
        _settings.Save();
    }

    private void OnFullscreenChanged(object? sender, DataChangedEventArgs<bool> e)
    {
        ApplyFullscreen(e.NewValue);
    }

    private void ApplyFullscreen(bool fullscreen)
    {
        _windowSettings.Mode.Set(fullscreen ? WindowMode.Fullscreen : WindowMode.Maximized);
    }
}