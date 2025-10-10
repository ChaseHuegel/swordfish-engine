using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.Library.Types;

namespace WaywardBeyond.Client.Core.Configuration;

internal sealed class SettingsManager : IAutoActivate
{
    private readonly IWindowContext _windowContext;
    private readonly Settings _settings;
    
    public SettingsManager(in IWindowContext windowContext, in Settings settings)
    {
        _windowContext = windowContext;
        _settings = settings;

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
        if (fullscreen)
        {
            _windowContext.Fullscreen();
        }
        else
        {
            _windowContext.Maximize();
        }
    }
}